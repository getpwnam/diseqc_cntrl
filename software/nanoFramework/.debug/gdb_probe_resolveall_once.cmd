set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x0802d588
monitor reset run
continue
printf "RESOLVEALL_HIT\n"
quit
