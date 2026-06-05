#!/usr/bin/env python3

import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from mcu_check_pins import (
    NATIVE_BASE,
    PROFILE_ALIASES,
    SUPPORTED_BUILD_PROFILES,
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

    def test_profile_lists_match_native_build_script(self):
        build_script = (NATIVE_BASE.parent / 'toolchain' / 'build.sh').read_text(encoding='utf-8')

        def extract_case_entries(case_block):
            entries = []
            lines = case_block.splitlines()
            i = 0
            while i < len(lines):
                line = lines[i].strip()
                if not line or line.startswith('#') or not line.endswith(')'):
                    i += 1
                    continue

                pattern = line[:-1].strip()
                body_lines = []
                i += 1
                while i < len(lines):
                    current = lines[i].strip()
                    if current == ';;':
                        break
                    body_lines.append(lines[i])
                    i += 1
                entries.append((pattern, '\n'.join(body_lines)))
                i += 1
            return entries

        case_blocks = []
        marker = 'case "$BUILD_PROFILE" in'
        start = 0
        while True:
            marker_idx = build_script.find(marker, start)
            if marker_idx == -1:
                break
            block_start = marker_idx + len(marker)
            block_end = build_script.find('\nesac', block_start)
            if block_end == -1:
                break
            case_blocks.append(build_script[block_start:block_end])
            start = block_end + len('\nesac')
        self.assertGreaterEqual(len(case_blocks), 2)

        alias_block = case_blocks[0]
        alias_map = {}
        for pattern, body in extract_case_entries(alias_block):
            target = None
            for line in body.splitlines():
                line = line.strip()
                if line.startswith('BUILD_PROFILE="'):
                    target = line.split('"')[1]
                    break
            if not target:
                continue
            for alias in pattern.split('|'):
                alias_map[alias] = target

        profile_block = case_blocks[1]
        native_profiles = {name for name, _ in extract_case_entries(profile_block) if name != '*'}

        self.assertEqual(SUPPORTED_BUILD_PROFILES, native_profiles)
        self.assertEqual(PROFILE_ALIASES, alias_map)


if __name__ == '__main__':
    unittest.main()
