/**
 * @file diseqc_native.cpp
 * @brief Native DiSEqC driver implementation for nanoFramework
 */

#include "diseqc_native.h"
#include <string.h>
#include <math.h>

/* Global handles */
diseqc_handle_t g_diseqc;
motor_enable_handle_t g_motor;

/* PWM Configuration for 22kHz carrier */
// System clock = 168MHz, want 1MHz PWM clock for 1µs resolution
// PWM frequency = 1MHz / 45 ≈ 22.2kHz
static PWMConfig pwm_config = {
    1000000,    // 1MHz PWM clock frequency
    45,         // PWM period (45 ticks at 1MHz = ~22kHz)
    NULL,       // No period callback
    {
        {PWM_OUTPUT_ACTIVE_HIGH, NULL},  // Channel 0 (TIM4_CH1)
        {PWM_OUTPUT_DISABLED, NULL},
        {PWM_OUTPUT_DISABLED, NULL},
        {PWM_OUTPUT_DISABLED, NULL}
    },
    0,
    0
};

/* GPT Configuration for segment timing */
static GPTConfig gpt_config = {
    1000000,    // 1MHz timer frequency (1µs resolution)
    NULL,       // Callback set dynamically
    0,
    0
};

/* Forward declarations */
static void gpt_callback(GPTDriver *gptp);
static THD_WORKING_AREA(wa_diseqc_tx, 1024);
static THD_FUNCTION(diseqc_tx_thread, arg);
static uint8_t calculate_parity(uint8_t byte);
static void add_bit(bool bit_value);
static void add_byte_with_parity(uint8_t byte);

/**
 * @brief Initialize DiSEqC driver
 */
diseqc_status_t diseqc_init(PWMDriver *pwm_driver, GPTDriver *gpt_driver)
{
    if (pwm_driver == NULL || gpt_driver == NULL) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    memset(&g_diseqc, 0, sizeof(diseqc_handle_t));
    
    g_diseqc.pwm_driver = pwm_driver;
    g_diseqc.gpt_driver = gpt_driver;
    g_diseqc.carrier_duty = 22;  // ~50% duty cycle at period 45
    g_diseqc.max_angle = 80.0f;
    g_diseqc.is_transmitting = false;
    
    // Initialize semaphore
    chBSemObjectInit(&g_diseqc.tx_complete_sem, false);
    
    // Start PWM driver
    pwmStart(pwm_driver, &pwm_config);
    pwmEnableChannel(pwm_driver, 0, 0);  // Start with carrier OFF
    
    // Start GPT driver
    gpt_config.callback = gpt_callback;
    gptStart(gpt_driver, &gpt_config);
    
    // Create transmission thread
    g_diseqc.tx_thread = chThdCreateStatic(wa_diseqc_tx, sizeof(wa_diseqc_tx),
                                           NORMALPRIO + 1, diseqc_tx_thread, NULL);
    
    return DISEQC_OK;
}

/**
 * @brief Calculate even parity bit
 */
static uint8_t calculate_parity(uint8_t byte)
{
    uint8_t parity = 0;
    for (int i = 0; i < 8; i++) {
        parity ^= (byte >> i) & 1;
    }
    return parity;
}

/**
 * @brief Add a bit to transmission buffer
 */
static void add_bit(bool bit_value)
{
    uint16_t high_duration, low_duration;
    
    if (bit_value) {
        // Bit '1': 500µs ON, 1000µs OFF
        high_duration = DISEQC_BIT1_HIGH_US;
        low_duration = DISEQC_BIT1_LOW_US;
    } else {
        // Bit '0': 1000µs ON, 500µs OFF
        high_duration = DISEQC_BIT0_HIGH_US;
        low_duration = DISEQC_BIT0_LOW_US;
    }
    
    // Add ON segment
    g_diseqc.segments[g_diseqc.segment_count].ccr_value = g_diseqc.carrier_duty;
    g_diseqc.segments[g_diseqc.segment_count].duration_us = high_duration;
    g_diseqc.segment_count++;
    
    // Add OFF segment
    g_diseqc.segments[g_diseqc.segment_count].ccr_value = 0;
    g_diseqc.segments[g_diseqc.segment_count].duration_us = low_duration;
    g_diseqc.segment_count++;
}

/**
 * @brief Add byte with parity to buffer
 */
static void add_byte_with_parity(uint8_t byte)
{
    // Add 8 data bits (MSB first)
    for (int i = 7; i >= 0; i--) {
        add_bit((byte >> i) & 1);
    }
    
    // Add parity bit (DiSEqC uses odd parity transmission)
    uint8_t parity = calculate_parity(byte);
    add_bit(parity == 0 ? 1 : 0);  // Even parity → send '1'
}

/**
 * @brief GPT callback - advances to next segment
 */
static void gpt_callback(GPTDriver *gptp)
{
    (void)gptp;
    
    chSysLockFromISR();
    
    // Signal transmission thread to continue
    chBSemSignalI(&g_diseqc.tx_complete_sem);
    
    chSysUnlockFromISR();
}

/**
 * @brief Transmission thread
 */
static THD_FUNCTION(diseqc_tx_thread, arg)
{
    (void)arg;
    
    chRegSetThreadName("diseqc_tx");
    
    while (true) {
        // Wait for transmission to start
        chSysLock();
        while (!g_diseqc.is_transmitting) {
            chSchGoSleepS(CH_STATE_SUSPENDED);
        }
        chSysUnlock();
        
        // Transmit all segments
        for (g_diseqc.segment_index = 0; 
             g_diseqc.segment_index < g_diseqc.segment_count; 
             g_diseqc.segment_index++) {
            
            diseqc_segment_t *seg = &g_diseqc.segments[g_diseqc.segment_index];
            
            // Update PWM duty cycle
            pwmEnableChannel(g_diseqc.pwm_driver, 0, seg->ccr_value);
            
            // Start GPT for segment duration
            gptStartOneShot(g_diseqc.gpt_driver, seg->duration_us);
            
            // Wait for segment to complete
            chBSemWait(&g_diseqc.tx_complete_sem);
        }
        
        // Transmission complete
        pwmEnableChannel(g_diseqc.pwm_driver, 0, 0);  // Carrier OFF
        g_diseqc.is_transmitting = false;
    }
}

/**
 * @brief Transmit DiSEqC command bytes
 */
diseqc_status_t diseqc_transmit(const uint8_t *data, uint8_t length)
{
    if (data == NULL || length == 0 || length > DISEQC_MAX_BYTES) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    if (g_diseqc.is_transmitting) {
        return DISEQC_ERROR_BUSY;
    }
    
    // Build transmission buffer
    g_diseqc.segment_count = 0;
    
    for (uint8_t i = 0; i < length; i++) {
        add_byte_with_parity(data[i]);
    }
    
    if (g_diseqc.segment_count == 0) {
        return DISEQC_ERROR_INVALID_PARAM;
    }
    
    // Start transmission
    g_diseqc.segment_index = 0;
    g_diseqc.is_transmitting = true;
    
    chSysLock();
    chSchWakeupS(g_diseqc.tx_thread, MSG_OK);
    chSysUnlock();
    
    return DISEQC_OK;
}

/**
 * @brief Send GotoX command
 */
diseqc_status_t diseqc_goto_angle(float angle)
{
    // Clamp angle
    if (angle > g_diseqc.max_angle) angle = g_diseqc.max_angle;
    if (angle < -g_diseqc.max_angle) angle = -g_diseqc.max_angle;
    
    // Build DiSEqC 1.2 GotoX command
    uint8_t cmd[5];
    
    cmd[0] = 0xE0;  // Command from master, no reply
    cmd[1] = 0x31;  // Any positioner
    cmd[2] = 0x6E;  // GotoX
    
    // Calculate position (angle * 16)
    uint8_t direction = (angle < 0) ? 0xE0 : 0xD0;
    int16_t angle_16 = (int16_t)(16.0f * fabsf(angle) + 0.5f);
    
    cmd[3] = direction | ((angle_16 >> 8) & 0x0F);
    cmd[4] = angle_16 & 0xFF;
    
    diseqc_status_t status = diseqc_transmit(cmd, 5);
    
    if (status == DISEQC_OK) {
        g_diseqc.current_angle = angle;
    }
    
    return status;
}

/**
 * @brief Send halt command
 */
diseqc_status_t diseqc_halt(void)
{
    uint8_t cmd[3] = {0xE0, 0x31, 0x60};
    return diseqc_transmit(cmd, 3);
}

/**
 * @brief Drive motor East (continuous)
 */
diseqc_status_t diseqc_drive_east(void)
{
    uint8_t cmd[4] = {0xE0, 0x31, 0x68, 0x00};  // Drive East, continuous
    return diseqc_transmit(cmd, 4);
}

/**
 * @brief Drive motor West (continuous)
 */
diseqc_status_t diseqc_drive_west(void)
{
    uint8_t cmd[4] = {0xE0, 0x31, 0x69, 0x00};  // Drive West, continuous
    return diseqc_transmit(cmd, 4);
}

/**
 * @brief Step motor East
 */
diseqc_status_t diseqc_step_east(uint8_t steps)
{
    if (steps == 0 || steps > 128) {
        return DISEQC_ERROR_INVALID_PARAM;
    }

    uint8_t cmd[4] = {0xE0, 0x31, 0x68, steps};  // Drive East, N steps
    return diseqc_transmit(cmd, 4);
}

/**
 * @brief Step motor West
 */
diseqc_status_t diseqc_step_west(uint8_t steps)
{
    if (steps == 0 || steps > 128) {
        return DISEQC_ERROR_INVALID_PARAM;
    }

    uint8_t cmd[4] = {0xE0, 0x31, 0x69, steps};  // Drive West, N steps
    return diseqc_transmit(cmd, 4);
}

/**
 * @brief Check if busy
 */
bool diseqc_is_busy(void)
{
    return g_diseqc.is_transmitting;
}

/**
 * @brief Get current angle
 */
float diseqc_get_current_angle(void)
{
    return g_diseqc.current_angle;
}

/* ========================================================================
 * Motor Enable Functions
 * ======================================================================== */

static void motor_timeout_callback(void *arg);

/**
 * @brief Initialize motor enable
 */
diseqc_status_t motor_enable_init(ioline_t enable_line)
{
    memset(&g_motor, 0, sizeof(motor_enable_handle_t));
    
    g_motor.enable_line = enable_line;
    g_motor.tracking_mode = false;
    g_motor.motor_on = false;
    
    // Initialize virtual timer
    chVTObjectInit(&g_motor.timeout_timer);
    
    // Set motor OFF initially
    palClearLine(g_motor.enable_line);
    
    return DISEQC_OK;
}

/**
 * @brief Motor timeout callback
 */
static void motor_timeout_callback(void *arg)
{
    (void)arg;
    
    if (!g_motor.tracking_mode) {
        palClearLine(g_motor.enable_line);
        g_motor.motor_on = false;
    }
}

/**
 * @brief Turn on motor for duration
 */
void motor_enable_turn_on(uint32_t travel_time_sec)
{
    if (g_motor.tracking_mode) {
        return;  // Don't override tracking mode
    }
    
    // Cancel any existing timeout
    chVTReset(&g_motor.timeout_timer);
    
    // Turn on motor
    palSetLine(g_motor.enable_line);
    g_motor.motor_on = true;
    
    // Set timeout (travel time + startup time)
    uint32_t total_time_ms = (travel_time_sec * 1000) + MOTOR_STARTUP_TIME_MS;
    chVTSet(&g_motor.timeout_timer, TIME_MS2I(total_time_ms), 
            motor_timeout_callback, NULL);
    
    // Block for startup time
    chThdSleepMilliseconds(MOTOR_STARTUP_TIME_MS);
}

/**
 * @brief Start tracking mode
 */
void motor_enable_start_tracking(void)
{
    chVTReset(&g_motor.timeout_timer);
    g_motor.tracking_mode = true;
    palSetLine(g_motor.enable_line);
    g_motor.motor_on = true;
}

/**
 * @brief Stop tracking mode
 */
void motor_enable_stop_tracking(void)
{
    g_motor.tracking_mode = false;
    palClearLine(g_motor.enable_line);
    g_motor.motor_on = false;
}

/**
 * @brief Force motor off
 */
void motor_enable_force_off(void)
{
    chVTReset(&g_motor.timeout_timer);
    g_motor.tracking_mode = false;
    palClearLine(g_motor.enable_line);
    g_motor.motor_on = false;
}

/**
 * @brief Check if motor is on
 */
bool motor_enable_is_on(void)
{
    return g_motor.motor_on;
}
