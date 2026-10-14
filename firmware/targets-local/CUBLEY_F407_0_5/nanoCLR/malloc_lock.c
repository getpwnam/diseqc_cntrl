//
// Copyright (c) 2026 Cubley
// See LICENSE file in the project root for full license information.
//

// Thread-safe newlib heap for CUBLEY_F407_0_5.
//
// lwIP (MEM_LIBC_MALLOC + MEMP_MEM_MALLOC) and mbedTLS both allocate from the
// newlib heap, from different ChibiOS threads. The toolchain's newlib ships the
// retargetable lock hooks as empty stubs, so without these overrides malloc/free
// are not serialised and a preempted allocation corrupts the heap (observed as a
// zeroed live TCP PCB tripping the lwIP "active pcb->state != CLOSED" assert).
//
// Defining __malloc_lock/__malloc_unlock here keeps newlib's mlock.o out of the
// link, so these implementations are the ones used by malloc, free, realloc and
// calloc.

#include <ch.h>
#include <reent.h>

static MUTEX_DECL(cubleyMallocMutex);
static thread_t *volatile cubleyMallocOwner;
static uint32_t cubleyMallocDepth;

static bool CubleyMallocLockRequired(void)
{
    if (ch_system.state != ch_sys_running)
    {
        // Before the kernel runs there is only one execution context.
        return false;
    }

    if (port_is_isr_context())
    {
        chSysHalt("malloc from ISR");
    }

    if (__get_PRIMASK() != 0U || port_is_locked(port_get_lock_status()))
    {
        // Blocking on the mutex is illegal here, and skipping the lock would
        // reintroduce the heap race; fail loudly instead.
        chSysHalt("malloc in critical section");
    }

    return true;
}

void __malloc_lock(struct _reent *reent)
{
    (void)reent;

    if (!CubleyMallocLockRequired())
    {
        return;
    }

    thread_t *self = chThdGetSelfX();

    if (cubleyMallocOwner == self)
    {
        cubleyMallocDepth++;
        return;
    }

    chMtxLock(&cubleyMallocMutex);
    cubleyMallocOwner = self;
    cubleyMallocDepth = 1U;
}

void __malloc_unlock(struct _reent *reent)
{
    (void)reent;

    if (!CubleyMallocLockRequired())
    {
        return;
    }

    chDbgAssert(cubleyMallocOwner == chThdGetSelfX(), "malloc unlock by non-owner");

    if (--cubleyMallocDepth == 0U)
    {
        cubleyMallocOwner = NULL;
        chMtxUnlock(&cubleyMallocMutex);
    }
}
