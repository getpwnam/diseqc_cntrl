set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x0802e854
monitor reset run
continue
printf "PREPAREFOREXEC_HIT\n"
quit
