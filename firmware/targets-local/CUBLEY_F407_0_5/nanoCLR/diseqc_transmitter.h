#ifndef DISEQC_TRANSMITTER_H
#define DISEQC_TRANSMITTER_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum
{
    DISEQC_TX_OK = 0,
    DISEQC_TX_INVALID_PARAM = 1,
    DISEQC_TX_BUSY = 2,
    DISEQC_TX_TIMER_UNAVAILABLE = 3,
    DISEQC_TX_CARRIER_UNAVAILABLE = 4,
    DISEQC_TX_TIMEOUT = 5,
} diseqc_tx_status_t;

diseqc_tx_status_t diseqc_transmit_frame(const uint8_t *frame, size_t length);
diseqc_tx_status_t diseqc_set_tone(uint32_t frequencyHz, uint32_t dutyPercent, bool enabled);
diseqc_tx_status_t diseqc_set_envelope_idle(bool high);

#ifdef __cplusplus
}
#endif

#endif // DISEQC_TRANSMITTER_H