#!/usr/bin/env python3

import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from mcu_check_pins import (
    NATIVE_BASE,
    infer_pin_requirements,
    normalize_profile,
    parse_board_pin_config,
    parse_native_override_mappings,
)


class McuCheckPinsTests(unittest.TestCase):
    def test_normalize_profile_alias(self):
        self.assertEqual(normalize_profile('minimal'), 'cubley-stable')
        self.assertEqual(normalize_profile('cubley-usb'), 'cubley-usb')

    def test_infer_i2c_requirements(self):
        req = infer_pin_requirements('I2C1_SCL', 'PB6', 'cubley-stable')
        self.assertEqual(req.get('mode'), 'ALTERNATE')
        self.assertEqual(req.get('otype'), 'OPENDRAIN')
        self.assertEqual(req.get('af'), 4)

    def test_usb_requirement_depends_on_profile(self):
        self.assertEqual(infer_pin_requirements('USB_D+', 'PA12', 'cubley-stable'), {})
        req = infer_pin_requirements('USB_D+', 'PA12', 'cubley-usb')
        self.assertEqual(req.get('mode'), 'ALTERNATE')
        self.assertEqual(req.get('af'), 10)

    def test_hardalive_usart_is_output(self):
        req = infer_pin_requirements('USART3_TX', 'PB10', 'cubley-hardalive')
        self.assertEqual(req.get('mode'), 'OUTPUT')
        self.assertEqual(req.get('otype'), 'PUSHPULL')
        self.assertNotIn('af', req)

    def test_board_pin_config_parsing(self):
        cfg = parse_board_pin_config(NATIVE_BASE / 'board_cubley.h')
        self.assertEqual(cfg['PB6'].get('mode'), 'ALTERNATE')
        self.assertEqual(cfg['PB6'].get('otype'), 'OPENDRAIN')
        self.assertEqual(cfg['PB6'].get('af'), 4)
        self.assertEqual(cfg['PB12'].get('mode'), 'OUTPUT')

    def test_native_override_parsing(self):
        maps = parse_native_override_mappings()
        self.assertEqual(maps['i2c']['I2C1']['SCL'][0], 'PB6')
        self.assertEqual(maps['uart']['USART3']['TX'][0], 'PB10')
        spi2_pins = {pin for pin, _ in maps['spi']['SPI2']}
        self.assertTrue({'PB13', 'PB14', 'PB15'}.issubset(spi2_pins))


if __name__ == '__main__':
    unittest.main()
