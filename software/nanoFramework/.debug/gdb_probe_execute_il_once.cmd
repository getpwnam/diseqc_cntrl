set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x08009788
monitor reset run
continue
printf "EXECUTE_IL_HIT\n"
quit
