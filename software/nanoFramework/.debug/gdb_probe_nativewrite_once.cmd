set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x0801f5bc
monitor reset run
continue
printf "NATIVE_WRITE_HIT\n"
quit
