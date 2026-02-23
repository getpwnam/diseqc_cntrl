#!/usr/bin/env python3
"""Parse KiCad 9 schematic files (.kicad_sch) to extract MCU pin-to-net mappings
and detect pin conflicts for the STM32F407VGT6.

KiCad coordinate convention:
  - Symbol library: Y increases UPWARD (math convention)
  - Schematic: Y increases DOWNWARD (screen convention)
  - When placing a symbol at (sx, sy) on schematic, a pin at lib position (px, py)
    maps to absolute (sx + px, sy - py) on the schematic.
"""

import re
import math
from pathlib import Path
from collections import defaultdict

BASE = Path("/home/cp/Dev/diseqc_cntrl/hardware/kicad-project")
TOLERANCE = 0.05

# ---- STM32F407VGT6 Alternate Function Table ----
STM32_AF_MAP = {
    'PA0':  [(0,'TIM2_CH1/TIM2_ETR'),(1,'TIM5_CH1'),(2,'TIM8_ETR'),(7,'USART2_CTS'),(8,'UART4_TX'),(11,'ETH_MII_CRS')],
    'PA1':  [(0,'TIM2_CH2'),(1,'TIM5_CH2'),(7,'USART2_RTS'),(8,'UART4_RX'),(11,'ETH_MII_RX_CLK/ETH_RMII_REF_CLK')],
    'PA2':  [(0,'TIM2_CH3'),(1,'TIM5_CH3'),(2,'TIM9_CH1'),(7,'USART2_TX'),(11,'ETH_MDIO')],
    'PA3':  [(0,'TIM2_CH4'),(1,'TIM5_CH4'),(2,'TIM9_CH2'),(7,'USART2_RX'),(10,'OTG_HS_ULPI_D0')],
    'PA4':  [(5,'SPI1_NSS'),(6,'SPI3_NSS/I2S3_WS'),(7,'USART2_CK'),(12,'OTG_HS_SOF'),(13,'DCMI_HSYNC')],
    'PA5':  [(0,'TIM2_CH1/TIM2_ETR'),(1,'TIM8_CH1N'),(5,'SPI1_SCK')],
    'PA6':  [(0,'TIM1_BKIN'),(1,'TIM8_BKIN'),(2,'TIM3_CH1'),(5,'SPI1_MISO'),(9,'TIM13_CH1'),(13,'DCMI_PIXCLK')],
    'PA7':  [(0,'TIM1_CH1N'),(1,'TIM8_CH1N'),(2,'TIM3_CH2'),(5,'SPI1_MOSI'),(9,'TIM14_CH1'),(11,'ETH_MII_RX_DV/ETH_RMII_CRS_DV')],
    'PA8':  [(0,'MCO1'),(1,'TIM1_CH1'),(4,'I2C3_SCL'),(7,'USART1_CK'),(10,'OTG_FS_SOF')],
    'PA9':  [(1,'TIM1_CH2'),(4,'I2C3_SMBA'),(7,'USART1_TX'),(13,'DCMI_D0')],
    'PA10': [(1,'TIM1_CH3'),(7,'USART1_RX'),(10,'OTG_FS_ID'),(13,'DCMI_D1')],
    'PA11': [(1,'TIM1_CH4'),(7,'USART1_CTS'),(9,'CAN1_RX'),(10,'OTG_FS_DM'),(14,'USART6_TX')],
    'PA12': [(1,'TIM1_ETR'),(7,'USART1_RTS'),(9,'CAN1_TX'),(10,'OTG_FS_DP'),(14,'USART6_RX')],
    'PA13': [(0,'JTMS/SWDIO')],
    'PA14': [(0,'JTCK/SWCLK')],
    'PA15': [(0,'JTDI'),(1,'TIM2_CH1/TIM2_ETR'),(5,'SPI1_NSS'),(6,'SPI3_NSS/I2S3_WS')],
    'PB0':  [(1,'TIM1_CH2N'),(2,'TIM3_CH3'),(3,'TIM8_CH2N'),(11,'ETH_MII_RXD2')],
    'PB1':  [(1,'TIM1_CH3N'),(2,'TIM3_CH4'),(3,'TIM8_CH3N'),(11,'ETH_MII_RXD3')],
    'PB2':  [],  # BOOT1
    'PB3':  [(0,'JTDO/TRACESWO'),(1,'TIM2_CH2'),(5,'SPI1_SCK'),(6,'SPI3_SCK/I2S3_CK')],
    'PB4':  [(0,'NJTRST'),(2,'TIM3_CH1'),(5,'SPI1_MISO'),(6,'SPI3_MISO'),(7,'I2S3ext_SD')],
    'PB5':  [(2,'TIM3_CH2'),(4,'I2C1_SMBA'),(5,'SPI1_MOSI'),(6,'SPI3_MOSI/I2S3_SD'),(9,'CAN2_RX'),(12,'OTG_HS_ULPI_D7')],
    'PB6':  [(2,'TIM4_CH1'),(4,'I2C1_SCL'),(5,'USART1_TX'),(9,'CAN2_TX'),(13,'DCMI_D5')],
    'PB7':  [(2,'TIM4_CH2'),(4,'I2C1_SDA'),(5,'USART1_RX'),(12,'FMC_NL'),(13,'DCMI_VSYNC')],
    'PB8':  [(2,'TIM4_CH3'),(3,'TIM10_CH1'),(4,'I2C1_SCL'),(9,'CAN1_RX'),(11,'ETH_MII_TXD3'),(12,'SDIO_D4')],
    'PB9':  [(2,'TIM4_CH4'),(3,'TIM11_CH1'),(4,'I2C1_SDA'),(5,'SPI2_NSS/I2S2_WS'),(9,'CAN1_TX'),(12,'SDIO_D5')],
    'PB10': [(1,'TIM2_CH3'),(4,'I2C2_SCL'),(5,'SPI2_SCK/I2S2_CK'),(7,'USART3_TX'),(10,'OTG_HS_ULPI_D3'),(11,'ETH_MII_RX_ER')],
    'PB11': [(1,'TIM2_CH4'),(4,'I2C2_SDA'),(7,'USART3_RX'),(10,'OTG_HS_ULPI_D4'),(11,'ETH_MII_TX_EN/ETH_RMII_TX_EN')],
    'PB12': [(1,'TIM1_BKIN'),(4,'I2C2_SMBA'),(5,'SPI2_NSS/I2S2_WS'),(7,'USART3_CK'),(9,'CAN2_RX'),(10,'OTG_HS_ULPI_D5'),(11,'ETH_MII_TXD0/ETH_RMII_TXD0'),(12,'OTG_HS_ID')],
    'PB13': [(1,'TIM1_CH1N'),(5,'SPI2_SCK/I2S2_CK'),(7,'USART3_CTS'),(9,'CAN2_TX'),(10,'OTG_HS_ULPI_D6'),(11,'ETH_MII_TXD1/ETH_RMII_TXD1')],
    'PB14': [(1,'TIM1_CH2N'),(3,'TIM8_CH2N'),(5,'SPI2_MISO'),(6,'I2S2ext_SD'),(7,'USART3_RTS'),(9,'TIM12_CH1'),(10,'OTG_HS_DM')],
    'PB15': [(0,'RTC_REFIN'),(1,'TIM1_CH3N'),(3,'TIM8_CH3N'),(5,'SPI2_MOSI/I2S2_SD'),(9,'TIM12_CH2'),(10,'OTG_HS_DP')],
    'PC0':  [(10,'OTG_HS_ULPI_STP')],
    'PC1':  [(11,'ETH_MDC')],
    'PC2':  [(5,'SPI2_MISO'),(6,'I2S2ext_SD'),(10,'OTG_HS_ULPI_DIR'),(11,'ETH_MII_TXD2')],
    'PC3':  [(5,'SPI2_MOSI/I2S2_SD'),(10,'OTG_HS_ULPI_NXT'),(11,'ETH_MII_TX_CLK')],
    'PC4':  [(11,'ETH_MII_RXD0/ETH_RMII_RXD0')],
    'PC5':  [(11,'ETH_MII_RXD1/ETH_RMII_RXD1')],
    'PC6':  [(2,'TIM3_CH1'),(3,'TIM8_CH1'),(5,'I2S2_MCK'),(8,'USART6_TX'),(12,'SDIO_D6'),(13,'DCMI_D0')],
    'PC7':  [(2,'TIM3_CH2'),(3,'TIM8_CH2'),(6,'I2S3_MCK'),(8,'USART6_RX'),(12,'SDIO_D7'),(13,'DCMI_D1')],
    'PC8':  [(2,'TIM3_CH3'),(3,'TIM8_CH3'),(7,'USART6_CK'),(12,'SDIO_D0'),(13,'DCMI_D2')],
    'PC9':  [(0,'MCO2'),(2,'TIM3_CH4'),(3,'TIM8_CH4'),(4,'I2C3_SDA'),(5,'I2S_CKIN'),(12,'SDIO_D1'),(13,'DCMI_D3')],
    'PC10': [(6,'SPI3_SCK/I2S3_CK'),(7,'USART3_TX'),(8,'UART4_TX'),(12,'SDIO_D2'),(13,'DCMI_D8')],
    'PC11': [(5,'I2S3ext_SD'),(6,'SPI3_MISO'),(7,'USART3_RX'),(8,'UART4_RX'),(12,'SDIO_D3'),(13,'DCMI_D4')],
    'PC12': [(6,'SPI3_MOSI/I2S3_SD'),(7,'USART3_CK'),(8,'UART5_TX'),(12,'SDIO_CK')],
    'PC13': [],
    'PC14': [],
    'PC15': [],
    'PD0':  [(9,'CAN1_RX'),(12,'FMC_D2')],
    'PD1':  [(9,'CAN1_TX'),(12,'FMC_D3')],
    'PD2':  [(2,'TIM3_ETR'),(8,'UART5_RX'),(12,'SDIO_CMD')],
    'PD3':  [(5,'SPI2_SCK/I2S2_CK'),(7,'USART2_CTS'),(12,'FMC_CLK'),(13,'DCMI_D5')],
    'PD4':  [(7,'USART2_RTS'),(12,'FMC_NOE')],
    'PD5':  [(7,'USART2_TX'),(12,'FMC_NWE')],
    'PD6':  [(5,'SPI3_MOSI/I2S3_SD'),(7,'USART2_RX'),(12,'FMC_NWAIT'),(13,'DCMI_D10')],
    'PD7':  [(7,'USART2_CK'),(12,'FMC_NE1/FMC_NCE2')],
    'PD8':  [(7,'USART3_TX'),(12,'FMC_D13')],
    'PD9':  [(7,'USART3_RX'),(12,'FMC_D14')],
    'PD10': [(7,'USART3_CK'),(12,'FMC_D15')],
    'PD11': [(7,'USART3_CTS'),(12,'FMC_A16/FMC_CLE')],
    'PD12': [(2,'TIM4_CH1'),(7,'USART3_RTS'),(12,'FMC_A17/FMC_ALE')],
    'PD13': [(2,'TIM4_CH2'),(12,'FMC_A18')],
    'PD14': [(2,'TIM4_CH3'),(12,'FMC_D0')],
    'PD15': [(2,'TIM4_CH4'),(12,'FMC_D1')],
    'PE0':  [(2,'TIM4_ETR'),(12,'FMC_NBL0'),(13,'DCMI_D2')],
    'PE1':  [(12,'FMC_NBL1'),(13,'DCMI_D3')],
    'PE2':  [(0,'TRACECLK'),(5,'SPI4_SCK'),(11,'ETH_MII_TXD3'),(12,'FMC_A23')],
    'PE3':  [(0,'TRACED0'),(12,'FMC_A19')],
    'PE4':  [(0,'TRACED1'),(5,'SPI4_NSS'),(12,'FMC_A20'),(13,'DCMI_D4')],
    'PE5':  [(0,'TRACED2'),(3,'TIM9_CH1'),(5,'SPI4_MISO'),(12,'FMC_A21'),(13,'DCMI_D6')],
    'PE6':  [(0,'TRACED3'),(3,'TIM9_CH2'),(5,'SPI4_MOSI'),(12,'FMC_A22'),(13,'DCMI_D7')],
    'PE7':  [(1,'TIM1_ETR'),(12,'FMC_D4')],
    'PE8':  [(1,'TIM1_CH1N'),(12,'FMC_D5')],
    'PE9':  [(1,'TIM1_CH1'),(12,'FMC_D6')],
    'PE10': [(1,'TIM1_CH2N'),(12,'FMC_D7')],
    'PE11': [(1,'TIM1_CH2'),(5,'SPI4_NSS'),(12,'FMC_D8')],
    'PE12': [(1,'TIM1_CH3N'),(5,'SPI4_SCK'),(12,'FMC_D9')],
    'PE13': [(1,'TIM1_CH3'),(5,'SPI4_MISO'),(12,'FMC_D10')],
    'PE14': [(1,'TIM1_CH4'),(5,'SPI4_MOSI'),(12,'FMC_D11')],
    'PE15': [(1,'TIM1_BKIN'),(12,'FMC_D12')],
    'PH0':  [],
    'PH1':  [],
}

POWER_PINS = {'VDD_1','VDD_2','VDD_3','VDD_4','VDD_5','VDD_6',
              'VSS_1','VSS_2','VSS_3','VSS_4','VSSA','VDDA','VREF+',
              'VBAT','VCAP_1','VCAP_2','BOOT0','NRST'}


def tokenize_sexp(text):
    tokens = []
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c == '(':
            tokens.append('(')
            i += 1
        elif c == ')':
            tokens.append(')')
            i += 1
        elif c == '"':
            j = i + 1
            while j < n and text[j] != '"':
                if text[j] == '\\':
                    j += 1
                j += 1
            tokens.append(text[i:j+1])
            i = j + 1
        elif c in ' \t\n\r':
            i += 1
        else:
            j = i
            while j < n and text[j] not in '() \t\n\r"':
                j += 1
            tokens.append(text[i:j])
            i = j
    return tokens


def parse_sexp(tokens, idx=0):
    if tokens[idx] == '(':
        lst = []
        idx += 1
        while tokens[idx] != ')':
            elem, idx = parse_sexp(tokens, idx)
            lst.append(elem)
        return lst, idx + 1
    else:
        val = tokens[idx]
        if val.startswith('"') and val.endswith('"'):
            val = val[1:-1]
        return val, idx + 1


def parse_file(filepath):
    text = filepath.read_text(encoding='utf-8')
    tokens = tokenize_sexp(text)
    result, _ = parse_sexp(tokens, 0)
    return result


def find_all(sexp, tag):
    results = []
    if isinstance(sexp, list):
        if len(sexp) > 0 and sexp[0] == tag:
            results.append(sexp)
        for item in sexp:
            results.extend(find_all(item, tag))
    return results


def find_first(sexp, tag):
    if isinstance(sexp, list):
        if len(sexp) > 0 and sexp[0] == tag:
            return sexp
        for item in sexp:
            result = find_first(item, tag)
            if result is not None:
                return result
    return None


def get_prop(sexp, prop_name):
    for item in sexp:
        if isinstance(item, list) and len(item) >= 3 and item[0] == 'property' and item[1] == prop_name:
            return item[2]
    return None


def get_at(sexp):
    at = find_first(sexp, 'at')
    if at and len(at) >= 3:
        angle = float(at[3]) if len(at) > 3 else 0
        return (float(at[1]), float(at[2]), angle)
    return None


def get_symbol_info(sym):
    lib_id = ref = None
    pos = (0, 0)
    angle = 0
    mirror_x = mirror_y = False

    for item in sym:
        if isinstance(item, list):
            if item[0] == 'lib_id':
                lib_id = item[1]
            elif item[0] == 'property' and item[1] == 'Reference':
                ref = item[2]
            elif item[0] == 'at':
                pos = (float(item[1]), float(item[2]))
                if len(item) > 3:
                    angle = float(item[3])
            elif item[0] == 'mirror':
                for m in item[1:]:
                    if m == 'x': mirror_x = True
                    if m == 'y': mirror_y = True

    return lib_id, ref, pos, angle, mirror_x, mirror_y


def transform_pin_pos(pin_lib_pos, sym_pos, sym_angle, mirror_x, mirror_y):
    """Transform a pin's library position to absolute schematic coordinates."""
    px, py = pin_lib_pos

    # Negate Y to convert from lib coords (Y-up) to schematic coords (Y-down)
    py = -py

    # Apply mirror
    if mirror_x:
        py = -py
    if mirror_y:
        px = -px

    # Apply rotation
    rad = math.radians(-sym_angle)
    rx = px * math.cos(rad) - py * math.sin(rad)
    ry = px * math.sin(rad) + py * math.cos(rad)

    return (round(sym_pos[0] + rx, 4), round(sym_pos[1] + ry, 4))


def build_lib_symbols_map(tree):
    lib_syms = {}
    lib_symbols = find_first(tree, 'lib_symbols')
    if lib_symbols:
        for item in lib_symbols:
            if isinstance(item, list) and item[0] == 'symbol':
                lib_syms[item[1]] = item
    return lib_syms


def get_symbol_pins(sym, lib_symbols_map):
    lib_id, ref, sym_pos, angle, mirror_x, mirror_y = get_symbol_info(sym)
    if not lib_id:
        return []

    lib_sym = lib_symbols_map.get(lib_id)
    if not lib_sym:
        return []

    pins = []
    all_pins = find_all(lib_sym, 'pin')
    for pin_def in all_pins:
        pin_name = pin_number = None
        pin_pos = None

        for sub in pin_def:
            if isinstance(sub, list):
                if sub[0] == 'name': pin_name = sub[1]
                elif sub[0] == 'number': pin_number = sub[1]
                elif sub[0] == 'at': pin_pos = (float(sub[1]), float(sub[2]))

        if pin_name and pin_pos:
            abs_pos = transform_pin_pos(pin_pos, sym_pos, angle, mirror_x, mirror_y)
            pins.append({'name': pin_name, 'number': pin_number, 'abs_pos': abs_pos})

    return pins


def points_match(p1, p2, tol=TOLERANCE):
    return abs(p1[0] - p2[0]) < tol and abs(p1[1] - p2[1]) < tol


def point_on_wire(point, wire, tol=TOLERANCE):
    (x1, y1), (x2, y2) = wire
    px, py = point

    min_x, max_x = min(x1, x2) - tol, max(x1, x2) + tol
    min_y, max_y = min(y1, y2) - tol, max(y1, y2) + tol
    if px < min_x or px > max_x or py < min_y or py > max_y:
        return False

    dx, dy = x2 - x1, y2 - y1
    length = math.sqrt(dx*dx + dy*dy)
    if length < tol:
        return math.sqrt((px-x1)**2 + (py-y1)**2) < tol

    cross = abs((px - x1) * dy - (py - y1) * dx) / length
    return cross < tol


class UnionFind:
    def __init__(self):
        self.parent = {}

    def _key(self, point):
        return (round(point[0], 2), round(point[1], 2))

    def find(self, point):
        k = self._key(point)
        if k not in self.parent:
            self.parent[k] = k
        while self.parent[k] != k:
            self.parent[k] = self.parent[self.parent[k]]
            k = self.parent[k]
        return k

    def union(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.parent[ra] = rb


def parse_schematic(filepath):
    tree = parse_file(filepath)

    symbols = []
    labels = []
    global_labels = []
    wires = []
    no_connects = []

    for item in tree:
        if not isinstance(item, list):
            continue
        tag = item[0]

        if tag == 'symbol':
            symbols.append(item)
        elif tag == 'label':
            at = get_at(item)
            if at:
                labels.append((item[1], (at[0], at[1])))
        elif tag == 'global_label':
            at = get_at(item)
            if at:
                global_labels.append((item[1], (at[0], at[1])))
        elif tag == 'wire':
            pts = find_first(item, 'pts')
            if pts:
                xylist = []
                for sub in pts:
                    if isinstance(sub, list) and sub[0] == 'xy':
                        xylist.append((float(sub[1]), float(sub[2])))
                if len(xylist) == 2:
                    wires.append(xylist)
        elif tag == 'no_connect':
            at = get_at(item)
            if at:
                no_connects.append((at[0], at[1]))

    return tree, symbols, labels, global_labels, wires, no_connects


def build_connectivity(symbols, labels, global_labels, wires, no_connects, lib_symbols_map):
    uf = UnionFind()

    # Connect wire endpoints
    for w in wires:
        uf.union(w[0], w[1])

    # Collect all pin positions
    all_pin_points = []
    for sym in symbols:
        lib_id, ref, _, _, _, _ = get_symbol_info(sym)
        if not ref:
            continue
        pins = get_symbol_pins(sym, lib_symbols_map)
        for p in pins:
            all_pin_points.append((ref, p['name'], p['number'], p['abs_pos'], lib_id or ''))

    # Connect pins to wires
    for ref, pname, pnum, ppos, lid in all_pin_points:
        for w in wires:
            if points_match(ppos, w[0]) or points_match(ppos, w[1]):
                uf.union(ppos, w[0])
                break
            elif point_on_wire(ppos, w):
                uf.union(ppos, w[0])
                break

    # Connect labels to wires
    for lname, lpos in labels + global_labels:
        for w in wires:
            if points_match(lpos, w[0]) or points_match(lpos, w[1]):
                uf.union(lpos, w[0])
                break
            elif point_on_wire(lpos, w):
                uf.union(lpos, w[0])
                break

    # Direct pin-to-label connections
    for ref, pname, pnum, ppos, lid in all_pin_points:
        for lname, lpos in labels + global_labels:
            if points_match(ppos, lpos):
                uf.union(ppos, lpos)

    # Same-name labels share a net
    label_groups = defaultdict(list)
    for lname, lpos in labels:
        label_groups[lname].append(lpos)
    for lname, positions in label_groups.items():
        for i in range(1, len(positions)):
            uf.union(positions[0], positions[i])

    # Same-name global labels share a net
    glabel_groups = defaultdict(list)
    for lname, lpos in global_labels:
        glabel_groups[lname].append(lpos)
    for lname, positions in glabel_groups.items():
        for i in range(1, len(positions)):
            uf.union(positions[0], positions[i])

    # Build net names
    net_names = defaultdict(set)
    for lname, lpos in labels + global_labels:
        root = uf.find(lpos)
        net_names[root].add(lname)

    # Power symbols
    for sym in symbols:
        lib_id, ref, _, _, _, _ = get_symbol_info(sym)
        if not ref or not ref.startswith('#PWR'):
            continue
        value = get_prop(sym, 'Value')
        if value:
            pins = get_symbol_pins(sym, lib_symbols_map)
            for p in pins:
                root = uf.find(p['abs_pos'])
                net_names[root].add(value)

    # Assign synthetic names to unlabeled but connected roots.
    # This prevents valid unlabeled analog/oscillator nodes from being
    # interpreted as floating simply because they have no explicit net label.
    root_to_pins = defaultdict(list)
    for ref, pname, pnum, ppos, lid in all_pin_points:
        root = uf.find(ppos)
        root_to_pins[root].append((ref, pname, pnum))

    unlabeled_idx = 1
    for root in sorted(root_to_pins.keys()):
        if net_names.get(root):
            continue
        if len(root_to_pins[root]) >= 2:
            net_names[root].add(f'__UNLABELED_NET_{unlabeled_idx}')
            unlabeled_idx += 1

    # Map each pin to net names
    pin_nets = {}
    for ref, pname, pnum, ppos, lid in all_pin_points:
        root = uf.find(ppos)
        pin_nets[(ref, pname, pnum)] = (net_names.get(root, set()), ppos, lid)

    # No-connect set
    nc_pins = set()
    for nc_pos in no_connects:
        for ref, pname, pnum, ppos, lid in all_pin_points:
            if points_match(nc_pos, ppos):
                nc_pins.add((ref, pname, pnum))

    return pin_nets, nc_pins


def analyze_mcu(filepath):
    print(f"\n{'='*70}")
    print(f"MCU Pin Conflict Analysis: {filepath.name}")
    print(f"{'='*70}")

    tree, symbols, labels, global_labels, wires, no_connects = parse_schematic(filepath)
    lib_symbols_map = build_lib_symbols_map(tree)
    pin_nets, nc_pins = build_connectivity(symbols, labels, global_labels, wires, no_connects, lib_symbols_map)

    # Find MCU
    mcu_ref = None
    for sym in symbols:
        lib_id, ref, _, _, _, _ = get_symbol_info(sym)
        if lib_id and 'STM32' in lib_id:
            mcu_ref = ref
            break

    if not mcu_ref:
        print("  No STM32 MCU found.")
        return [], []

    # Collect MCU pins
    mcu_pins = {}
    for (ref, pname, pnum), (nets, ppos, lid) in pin_nets.items():
        if ref == mcu_ref:
            mcu_pins[(pname, pnum)] = (nets, ppos)

    print(f"\nMCU: {mcu_ref} (STM32F407VGT6)")
    print(f"Total pins: {len(mcu_pins)}")

    connected = []
    floating = []

    print(f"\n--- Pin Assignments (sorted by pin number) ---")
    for (pname, pnum), (nets, ppos) in sorted(mcu_pins.items(), key=lambda x: int(x[0][1])):
        net_str = ', '.join(sorted(nets)) if nets else '(unconnected)'
        is_power = pname in POWER_PINS
        is_nc = (mcu_ref, pname, pnum) in nc_pins

        suffix = ''
        if is_power:
            suffix = '  [POWER]'
        elif is_nc:
            suffix = '  [NO_CONNECT]'
        elif not nets:
            suffix = '  ** FLOATING **'
            floating.append((pname, pnum))
        else:
            connected.append((pname, pnum, nets))

        print(f"  Pin {pnum:>3s} {pname:<8s} -> {net_str}{suffix}")

    # ============================================================
    # PIN CONFLICT CHECKS
    # ============================================================

    conflicts = []
    warnings = []

    # 1. Multiple nets on same pin
    print(f"\n--- Check: Multiple Nets on Same Pin ---")
    found = False
    for (pname, pnum), (nets, _) in mcu_pins.items():
        if len(nets) > 1 and pname not in POWER_PINS:
            msg = f"Pin {pnum} ({pname}) connected to MULTIPLE nets: {', '.join(sorted(nets))}"
            conflicts.append(msg)
            print(f"  CONFLICT: {msg}")
            found = True
    if not found:
        print("  None found.")

    # 2. Same net on multiple MCU GPIO pins
    print(f"\n--- Check: Shared Nets Between MCU GPIO Pins ---")
    net_to_pins = defaultdict(list)
    for (pname, pnum), (nets, _) in mcu_pins.items():
        if pname not in POWER_PINS:
            for net in nets:
                net_to_pins[net].append((pname, pnum))

    shared_found = False
    for net, pins in sorted(net_to_pins.items()):
        if len(pins) > 1:
            pin_str = ', '.join(f'{pn}(pin {pnum})' for pn, pnum in pins)
            msg = f"Net '{net}' shared by: {pin_str}"
            warnings.append(msg)
            print(f"  WARNING: {msg}")
            shared_found = True
    if not shared_found:
        print("  None found.")

    # 3. Debug Interface (SWD)
    print(f"\n--- Check: Debug Interface (SWD) ---")
    for pname, pnum, nets in connected:
        for net in nets:
            nu = net.upper()
            if 'SWDIO' in nu:
                if pname == 'PA13':
                    print(f"  SWDIO on PA13 (pin {pnum}) -> AF0: JTMS/SWDIO - OK")
                else:
                    msg = f"SWDIO on {pname} (pin {pnum}) - WRONG! Should be PA13"
                    conflicts.append(msg)
                    print(f"  CONFLICT: {msg}")
            if 'SWCLK' in nu:
                if pname == 'PA14':
                    print(f"  SWCLK on PA14 (pin {pnum}) -> AF0: JTCK/SWCLK - OK")
                else:
                    msg = f"SWCLK on {pname} (pin {pnum}) - WRONG! Should be PA14"
                    conflicts.append(msg)
                    print(f"  CONFLICT: {msg}")
            if nu == 'SWO':
                if pname == 'PB3':
                    print(f"  SWO on PB3 (pin {pnum}) -> AF0: JTDO/TRACESWO - OK")
                else:
                    msg = f"SWO on {pname} (pin {pnum}) - WRONG! Should be PB3"
                    conflicts.append(msg)
                    print(f"  CONFLICT: {msg}")

    # 4. I2C
    print(f"\n--- Check: I2C Pin Assignments ---")
    i2c_found = defaultdict(dict)
    for pname, pnum, nets in connected:
        for net in nets:
            nu = net.upper()
            for bus in ['I2C1', 'I2C2', 'I2C3']:
                if bus in nu:
                    sig = 'SCL' if 'SCL' in nu else ('SDA' if 'SDA' in nu else None)
                    if sig:
                        i2c_found[bus][sig] = (pname, pnum, net)

    for bus, signals in sorted(i2c_found.items()):
        print(f"  {bus}:")
        for sig, (pname, pnum, net) in sorted(signals.items()):
            valid = False
            af_n = None
            if pname in STM32_AF_MAP:
                for af, func in STM32_AF_MAP[pname]:
                    if bus in func.upper() and sig in func.upper():
                        valid = True
                        af_n = af
                        break
            if valid:
                print(f"    {sig}: {pname} (pin {pnum}) -> AF{af_n} - OK")
            else:
                msg = f"{bus}_{sig} on {pname} (pin {pnum}) - NOT a valid {bus}_{sig} pin!"
                conflicts.append(msg)
                print(f"    ** CONFLICT: {msg}")

    # 5. USART/UART
    print(f"\n--- Check: USART/UART Pin Assignments ---")
    usart_found = defaultdict(dict)
    for pname, pnum, nets in connected:
        for net in nets:
            nu = net.upper()
            for bus in ['USART1','USART2','USART3','UART4','UART5','USART6']:
                if bus in nu:
                    if 'TX' in nu and 'RX' not in nu:
                        usart_found[bus]['TX'] = (pname, pnum, net)
                    elif 'RX' in nu:
                        usart_found[bus]['RX'] = (pname, pnum, net)

    for bus, signals in sorted(usart_found.items()):
        print(f"  {bus}:")
        for sig, (pname, pnum, net) in sorted(signals.items()):
            valid = False
            af_n = None
            if pname in STM32_AF_MAP:
                for af, func in STM32_AF_MAP[pname]:
                    fu = func.upper()
                    if (bus in fu or bus.replace('USART','UART') in fu):
                        if sig == 'TX' and 'TX' in fu:
                            valid = True; af_n = af; break
                        elif sig == 'RX' and 'RX' in fu:
                            valid = True; af_n = af; break
            if valid:
                print(f"    {sig}: {pname} (pin {pnum}) -> AF{af_n} - OK")
            else:
                msg = f"{bus}_{sig} on {pname} (pin {pnum}) - NOT a valid {bus}_{sig} pin!"
                conflicts.append(msg)
                print(f"    ** CONFLICT: {msg}")

    # 6. Timer
    print(f"\n--- Check: Timer Pin Assignments ---")
    tim_found = False
    for pname, pnum, nets in connected:
        for net in nets:
            m = re.match(r'(TIM\d+)_(CH\d+N?)', net.upper().replace('_DSQ',''))
            if m:
                tim_found = True
                tim, ch = m.group(1), m.group(2)
                valid = False
                af_n = None
                if pname in STM32_AF_MAP:
                    for af, func in STM32_AF_MAP[pname]:
                        if tim in func.upper() and ch in func.upper():
                            valid = True
                            af_n = af
                            break
                if valid:
                    print(f"  {net}: {pname} (pin {pnum}) -> AF{af_n} - OK")
                else:
                    msg = f"{net} on {pname} (pin {pnum}) - NOT a valid {tim}_{ch} pin!"
                    conflicts.append(msg)
                    print(f"  ** CONFLICT: {msg}")
    if not tim_found:
        print("  No timer connections found.")

    # 7. USB
    print(f"\n--- Check: USB Pin Assignments ---")
    for pname, pnum, nets in connected:
        for net in nets:
            nu = net.upper()
            if 'USB' in nu:
                if 'D+' in net or 'DP' in nu:
                    if pname in ('PA12', 'PB15'):
                        af_type = 'OTG_FS_DP' if pname == 'PA12' else 'OTG_HS_DP'
                        print(f"  USB D+ on {pname} (pin {pnum}) -> AF10: {af_type} - OK")
                    else:
                        msg = f"USB D+ on {pname} (pin {pnum}) - wrong pin"
                        warnings.append(msg)
                        print(f"  WARNING: {msg}")
                elif 'D-' in net or 'DN' in nu or 'DM' in nu:
                    if pname in ('PA11', 'PB14'):
                        af_type = 'OTG_FS_DM' if pname == 'PA11' else 'OTG_HS_DM'
                        print(f"  USB D- on {pname} (pin {pnum}) -> AF10: {af_type} - OK")
                    else:
                        msg = f"USB D- on {pname} (pin {pnum}) - wrong pin"
                        warnings.append(msg)
                        print(f"  WARNING: {msg}")

    # 8. HSE crystal
    print(f"\n--- Check: Oscillator Pins ---")
    ph0_nets = mcu_pins.get(('PH0', '12'), (set(), None))[0]
    ph1_nets = mcu_pins.get(('PH1', '13'), (set(), None))[0]
    ph0_nc = (mcu_ref, 'PH0', '12') in nc_pins
    ph1_nc = (mcu_ref, 'PH1', '13') in nc_pins
    if ph0_nets:
        print(f"  PH0/OSC_IN (pin 12) -> {', '.join(ph0_nets)}")
    elif ph0_nc:
        print(f"  PH0/OSC_IN (pin 12) -> NO_CONNECT")
    else:
        warnings.append("PH0/OSC_IN (pin 12) is floating - needs HSE crystal or NC")
        print(f"  WARNING: PH0/OSC_IN (pin 12) is FLOATING")
    if ph1_nets:
        print(f"  PH1/OSC_OUT (pin 13) -> {', '.join(ph1_nets)}")
    elif ph1_nc:
        print(f"  PH1/OSC_OUT (pin 13) -> NO_CONNECT")
    else:
        warnings.append("PH1/OSC_OUT (pin 13) is floating - needs HSE crystal or NC")
        print(f"  WARNING: PH1/OSC_OUT (pin 13) is FLOATING")

    # ============================================================
    # SUMMARY
    # ============================================================

    print(f"\n{'='*70}")
    print(f"SUMMARY")
    print(f"{'='*70}")
    print(f"  Total MCU pins: {len(mcu_pins)}")
    print(f"  Connected GPIO pins: {len(connected)}")
    print(f"  Floating GPIO pins: {len(floating)}")
    if floating:
        ports = defaultdict(list)
        for pname, pnum in floating:
            port = re.match(r'P[A-Z]', pname)
            port = port.group() if port else '?'
            ports[port].append(f"{pname}({pnum})")
        for port, pins in sorted(ports.items()):
            print(f"    {port}: {', '.join(pins)}")

    print(f"\n  CONFLICTS ({len(conflicts)}):")
    if conflicts:
        for c in conflicts:
            print(f"    [!] {c}")
    else:
        print(f"    None detected")

    print(f"\n  WARNINGS ({len(warnings)}):")
    if warnings:
        for w in warnings:
            print(f"    [~] {w}")
    else:
        print(f"    None")

    return conflicts, warnings


if __name__ == '__main__':
    mcu_sch = BASE / "diseqc_cntrl_mcu.kicad_sch"
    print("=" * 70)
    print("KiCad STM32F407VGT6 MCU Pin Conflict Analyzer")
    print("DiSEqC Motor Control Project")
    print("=" * 70)

    conflicts, warnings = analyze_mcu(mcu_sch)
