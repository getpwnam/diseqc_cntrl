set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x080320e0
monitor reset run
continue
printf "NEWTHREAD_HIT\n"
quit
