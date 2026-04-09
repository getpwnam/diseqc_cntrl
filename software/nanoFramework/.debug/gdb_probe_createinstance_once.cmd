set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x0801434c
monitor reset run
continue
printf "CREATEINSTANCE_HIT\n"
quit
