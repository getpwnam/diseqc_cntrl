set pagination off
set confirm off
target extended-remote :3333
monitor reset halt
break *0x0801f5d4
monitor reset run
continue
printf "NATIVEWRITE_ARGS "
info registers r5 r6
quit
