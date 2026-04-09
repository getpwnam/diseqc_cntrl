set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x08019d9c
monitor reset run
continue
printf "AFTER_RESOLVEALL "
info registers r0
quit
