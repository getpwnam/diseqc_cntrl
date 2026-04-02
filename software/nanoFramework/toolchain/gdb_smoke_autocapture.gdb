set pagination off
set confirm off
set print pretty on

# Connect and reset into a known state.
target extended-remote :3333
monitor reset halt

# Utility command to dump fault state quickly.
define dump_fault
  echo \n==== FAULT SNAPSHOT ====\n
  info registers
  x/4wx 0xE000ED28
  x/wx 0xE000ED2C
  x/wx 0xE000ED30
  x/wx 0xE000ED04
  x/12i $pc
  bt
  echo ==== END SNAPSHOT ====\n
end

tbreak main
continue

# Catch all common exception handlers and auto-dump when hit.
break HardFault_Handler
commands
  silent
  echo \n*** HardFault_Handler hit ***\n
  dump_fault
  quit
end

break MemManage_Handler
commands
  silent
  echo \n*** MemManage_Handler hit ***\n
  dump_fault
  quit
end

break BusFault_Handler
commands
  silent
  echo \n*** BusFault_Handler hit ***\n
  dump_fault
  quit
end

break UsageFault_Handler
commands
  silent
  echo \n*** UsageFault_Handler hit ***\n
  dump_fault
  quit
end

# Run and wait. If no fault happens, interrupt with Ctrl-C and run 'dump_fault'.
continue
