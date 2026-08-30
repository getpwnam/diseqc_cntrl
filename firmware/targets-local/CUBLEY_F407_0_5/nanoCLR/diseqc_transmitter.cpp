#include "diseqc_transmitter.h"

#include <ch.h>
#include <hal.h>

#define DISEQC_MIN_FRAME_BYTES 3U
#define DISEQC_MAX_FRAME_BYTES 6U
#define DISEQC_BITS_PER_BYTE 9U
#define DISEQC_SHORT_INTERVAL_US 500U
#define DISEQC_LONG_INTERVAL_US 1000U
#define DISEQC_TX_TIMEOUT_MS 250U
#define DISEQC_TIM4_CHANNEL 0U
#define DISEQC_CARRIER_PIN 12U
#define DISEQC_DEFAULT_FREQUENCY_HZ 22000U
#define DISEQC_DEFAULT_DUTY_PERCENT 50U
#define DISEQC_PWM_PERIOD_TICKS 100U

typedef enum
{
    DISEQC_PHASE_MARK,
    DISEQC_PHASE_SPACE,
} diseqc_phase_t;

static uint8_t g_diseqc_bits[DISEQC_MAX_FRAME_BYTES * DISEQC_BITS_PER_BYTE];
static size_t g_diseqc_bit_count;
static volatile size_t g_diseqc_bit_index;
static volatile diseqc_phase_t g_diseqc_phase;
static volatile bool g_diseqc_timer_active;
static bool g_diseqc_claimed;
static uint32_t g_diseqc_carrier_frequency_hz;
static uint32_t g_diseqc_carrier_duty_percent;
static PWMConfig g_diseqc_pwm_config;
static semaphore_t g_diseqc_complete;

static void diseqc_set_carrier_i(bool enabled)
{
    if (enabled)
    {
        pwmEnableChannelI(
            &PWMD4,
            DISEQC_TIM4_CHANNEL,
            (pwmcnt_t)((PWMD4.period * g_diseqc_carrier_duty_percent) / 100U));
    }
    else
    {
        pwmDisableChannelI(&PWMD4, DISEQC_TIM4_CHANNEL);
    }
}

static gptcnt_t diseqc_mark_interval(size_t bitIndex)
{
    return g_diseqc_bits[bitIndex] != 0U ? DISEQC_SHORT_INTERVAL_US : DISEQC_LONG_INTERVAL_US;
}

static gptcnt_t diseqc_space_interval(size_t bitIndex)
{
    return g_diseqc_bits[bitIndex] != 0U ? DISEQC_LONG_INTERVAL_US : DISEQC_SHORT_INTERVAL_US;
}

static void diseqc_timer_callback(GPTDriver *gptp)
{
    (void)gptp;

    if (!g_diseqc_timer_active)
    {
        diseqc_set_carrier_i(false);
        return;
    }

    if (g_diseqc_phase == DISEQC_PHASE_MARK)
    {
        diseqc_set_carrier_i(false);
        g_diseqc_phase = DISEQC_PHASE_SPACE;
        gptStartOneShotI(&GPTD6, diseqc_space_interval(g_diseqc_bit_index));
        return;
    }

    g_diseqc_bit_index = g_diseqc_bit_index + 1U;
    if (g_diseqc_bit_index >= g_diseqc_bit_count)
    {
        g_diseqc_timer_active = false;
        diseqc_set_carrier_i(false);
        chSemSignalI(&g_diseqc_complete);
        return;
    }

    g_diseqc_phase = DISEQC_PHASE_MARK;
    diseqc_set_carrier_i(true);
    gptStartOneShotI(&GPTD6, diseqc_mark_interval(g_diseqc_bit_index));
}

static const GPTConfig g_diseqc_timer_config = {
    1000000U,
    diseqc_timer_callback,
    0U,
    0U,
};

static bool diseqc_try_claim(void)
{
    bool claimed = false;

    chSysLock();
    if (!g_diseqc_claimed)
    {
        g_diseqc_claimed = true;
        claimed = true;
    }
    chSysUnlock();

    return claimed;
}

static void diseqc_release(void)
{
    chSysLock();
    g_diseqc_claimed = false;
    chSysUnlock();
}

static diseqc_tx_status_t diseqc_configure_carrier(uint32_t frequencyHz, uint32_t dutyPercent)
{
    if (frequencyHz < 1000U || frequencyHz > 100000U || dutyPercent == 0U || dutyPercent >= 100U)
    {
        return DISEQC_TX_INVALID_PARAM;
    }

    if (PWMD4.state == PWM_READY &&
        PWMD4.config == &g_diseqc_pwm_config &&
        g_diseqc_carrier_frequency_hz == frequencyHz &&
        g_diseqc_carrier_duty_percent == dutyPercent)
    {
        pwmDisableChannel(&PWMD4, DISEQC_TIM4_CHANNEL);
        return DISEQC_TX_OK;
    }

    if (PWMD4.state == PWM_READY)
    {
        if (PWMD4.config != &g_diseqc_pwm_config)
        {
            return DISEQC_TX_CARRIER_UNAVAILABLE;
        }

        pwmDisableChannel(&PWMD4, DISEQC_TIM4_CHANNEL);
        pwmStop(&PWMD4);
    }
    else if (PWMD4.state != PWM_STOP)
    {
        return DISEQC_TX_CARRIER_UNAVAILABLE;
    }

    g_diseqc_pwm_config = {
        frequencyHz * DISEQC_PWM_PERIOD_TICKS,
        DISEQC_PWM_PERIOD_TICKS,
        NULL,
        {
            {PWM_OUTPUT_ACTIVE_HIGH, NULL},
            {PWM_OUTPUT_ACTIVE_HIGH, NULL},
            {PWM_OUTPUT_ACTIVE_HIGH, NULL},
            {PWM_OUTPUT_ACTIVE_HIGH, NULL},
        },
        0U,
        0U,
        0U,
    };

    if (pwmStart(&PWMD4, &g_diseqc_pwm_config) != MSG_OK)
    {
        return DISEQC_TX_CARRIER_UNAVAILABLE;
    }

    palSetPadMode(GPIOD, DISEQC_CARRIER_PIN, PAL_MODE_ALTERNATE(2));
    pwmDisableChannel(&PWMD4, DISEQC_TIM4_CHANNEL);
    g_diseqc_carrier_frequency_hz = frequencyHz;
    g_diseqc_carrier_duty_percent = dutyPercent;
    return DISEQC_TX_OK;
}

static uint8_t diseqc_odd_parity_bit(uint8_t value)
{
    uint8_t ones = 0U;

    for (uint8_t bit = 0U; bit < 8U; bit++)
    {
        ones += (uint8_t)((value >> bit) & 0x01U);
    }

    return (ones & 0x01U) == 0U ? 1U : 0U;
}

static void diseqc_encode_frame(const uint8_t *frame, size_t length)
{
    size_t outputIndex = 0U;

    for (size_t byteIndex = 0U; byteIndex < length; byteIndex++)
    {
        const uint8_t value = frame[byteIndex];
        for (int bit = 7; bit >= 0; bit--)
        {
            g_diseqc_bits[outputIndex++] = (uint8_t)((value >> bit) & 0x01U);
        }

        g_diseqc_bits[outputIndex++] = diseqc_odd_parity_bit(value);
    }

    g_diseqc_bit_count = outputIndex;
}

diseqc_tx_status_t diseqc_transmit_frame(const uint8_t *frame, size_t length)
{
    if (frame == NULL || length < DISEQC_MIN_FRAME_BYTES || length > DISEQC_MAX_FRAME_BYTES)
    {
        return DISEQC_TX_INVALID_PARAM;
    }

    if (!diseqc_try_claim())
    {
        return DISEQC_TX_BUSY;
    }

    diseqc_tx_status_t status = diseqc_configure_carrier(
        DISEQC_DEFAULT_FREQUENCY_HZ,
        DISEQC_DEFAULT_DUTY_PERCENT);
    if (status != DISEQC_TX_OK)
    {
        diseqc_release();
        return status;
    }

    diseqc_encode_frame(frame, length);
    chSemObjectInit(&g_diseqc_complete, 0);

    if (GPTD6.state == GPT_STOP)
    {
        if (gptStart(&GPTD6, &g_diseqc_timer_config) != MSG_OK)
        {
            diseqc_release();
            return DISEQC_TX_TIMER_UNAVAILABLE;
        }
    }
    else if (GPTD6.state != GPT_READY || GPTD6.config != &g_diseqc_timer_config)
    {
        diseqc_release();
        return DISEQC_TX_TIMER_UNAVAILABLE;
    }

    g_diseqc_bit_index = 0U;
    g_diseqc_phase = DISEQC_PHASE_MARK;
    g_diseqc_timer_active = true;

    chSysLock();
    diseqc_set_carrier_i(true);
    gptStartOneShotI(&GPTD6, diseqc_mark_interval(0U));
    chSysUnlock();

    if (chSemWaitTimeout(&g_diseqc_complete, TIME_MS2I(DISEQC_TX_TIMEOUT_MS)) != MSG_OK)
    {
        chSysLock();
        if (GPTD6.state == GPT_ONESHOT)
        {
            gptStopTimerI(&GPTD6);
        }
        g_diseqc_timer_active = false;
        diseqc_set_carrier_i(false);
        chSysUnlock();
        diseqc_release();
        return DISEQC_TX_TIMEOUT;
    }

    diseqc_release();
    return DISEQC_TX_OK;
}

diseqc_tx_status_t diseqc_set_tone(uint32_t frequencyHz, uint32_t dutyPercent, bool enabled)
{
    if (!diseqc_try_claim())
    {
        return DISEQC_TX_BUSY;
    }

    const diseqc_tx_status_t status = diseqc_configure_carrier(frequencyHz, dutyPercent);
    if (status == DISEQC_TX_OK && enabled)
    {
        pwmEnableChannel(
            &PWMD4,
            DISEQC_TIM4_CHANNEL,
            (pwmcnt_t)((PWMD4.period * g_diseqc_carrier_duty_percent) / 100U));
    }

    diseqc_release();
    return status;
}

diseqc_tx_status_t diseqc_set_envelope_idle(bool high)
{
    if (!diseqc_try_claim())
    {
        return DISEQC_TX_BUSY;
    }

    if (PWMD4.state == PWM_READY)
    {
        if (PWMD4.config != &g_diseqc_pwm_config)
        {
            diseqc_release();
            return DISEQC_TX_CARRIER_UNAVAILABLE;
        }

        pwmDisableChannel(&PWMD4, DISEQC_TIM4_CHANNEL);
        pwmStop(&PWMD4);
    }
    else if (PWMD4.state != PWM_STOP)
    {
        diseqc_release();
        return DISEQC_TX_CARRIER_UNAVAILABLE;
    }

    // In LNBH26 internal-generator mode (EXTM=0, TEN=1), DSQIN is an
    // envelope gate. High requests continuous 22 kHz; low suppresses it.
    // Program the output latch before changing from the timer alternate
    // function so the LNB never sees an unintended gate pulse.
    palWritePad(GPIOD, DISEQC_CARRIER_PIN, high ? PAL_HIGH : PAL_LOW);
    palSetPadMode(GPIOD, DISEQC_CARRIER_PIN, PAL_MODE_OUTPUT_PUSHPULL);

    diseqc_release();
    return DISEQC_TX_OK;
}