# ConceptJewelleryPro — Barcode Sticker PRN Field Reference

This document provides the complete reference of all parameters and placeholders available when designing Zebra/TSC thermal printer templates (`.prn` files in ZPL format) for jewellery barbell / rat-tail stickers.

---

## 1. Sticker Specifications

| Property | Value | Notes |
| :--- | :--- | :--- |
| **Physical Sticker Size** | `81 mm x 12 mm` | Total length including 26mm rat-tail |
| **Printable Bat Width** | `55 mm` | Printable area spanning left and right flaps |
| **Printable Bat Height** | `12 mm` | Height of the printable bat |
| **Printer Resolution** | `203 DPI` | 8 dots per mm (440 dots width × 96 dots height) |
| **ZPL Dimensions Header** | `^PW440 ^LL96` | Printable width: 440 dots, Label length: 96 dots |
| **Left Flap Coordinate Range** | `X = 10 to 210` | 200 dots width for item info, weights, purity |
| **Right Flap Coordinate Range** | `X = 230 to 430` | 200 dots width for firm name, barcode, code text |

---

## 2. Template Selection Rules

The application selects the `.prn` template automatically based on the **`TYPE PC/WT`** field:
- If **`TYPE PC/WT`** is **`PC`** $\rightarrow$ Defaults to **`StickerPC.prn`**
- If **`TYPE PC/WT`** is **`WT`** (or `-`) $\rightarrow$ Defaults to **`StickerWt.prn`**
- Any custom format created as `<FormatName>.prn` in this folder is automatically detected and listed in the **Sticker Format** dropdown.

---

## 3. Complete Field Parameter Reference

All placeholder tags are **case-insensitive** and must be wrapped in double curly braces, e.g. `{{ITEM_NAME}}`.

### A. Weight Fields & Coded Weight

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{NW}}`** | **Coded Net Weight** (milligrams prefixed with 'A') | `A` + weight in mg without dot | **`A2300`** *(for 2.300g)*, **`A12500`** *(for 12.500g)* |
| **`{{CODED_NW}}`** | Coded Net Weight (alias for `{{NW}}`) | `A` + weight in mg without dot | **`A2300`** |
| **`{{CODED_NET_WT}}`** | Coded Net Weight (alias for `{{NW}}`) | `A` + weight in mg without dot | **`A2300`** |
| **`{{NET_WT}}`** | Literal Net Weight in grams | 3 decimal places (`0.000`) | **`2.300`** |
| **`{{NET_WEIGHT}}`** | Literal Net Weight in grams | 3 decimal places (`0.000`) | **`2.300`** |
| **`{{GROSS_WT}}`** | Gross Weight in grams | 3 decimal places (`0.000`) | **`2.450`** |
| **`{{GROSS_WEIGHT}}`**| Gross Weight in grams | 3 decimal places (`0.000`) | **`2.450`** |
| **`{{GW}}`** | Gross Weight in grams (short alias) | 3 decimal places (`0.000`) | **`2.450`** |
| **`{{STONE_WT}}`** | Stone / Less Weight in grams | 3 decimal places (`0.000`) | **`0.150`** |
| **`{{LESS_WT}}`** | Less Weight in grams | 3 decimal places (`0.000`) | **`0.150`** |
| **`{{LW}}`** | Less Weight in grams (short alias) | 3 decimal places (`0.000`) | **`0.150`** |

> **Coded Weight Example:**
> A piece with net weight `2.300g` will stream:
> - `{{NW}}` $\rightarrow$ `A2300`
> - `{{NET_WT}}` $\rightarrow$ `2.300`
>
> A piece with net weight `12.500g` will stream:
> - `{{NW}}` $\rightarrow$ `A12500`
> - `{{NET_WT}}` $\rightarrow$ `12.500`

---

### B. Barcode & Identification Fields

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{BARCODE}}`** | Barcode Number | Plain alphanumeric string | `009498` or `100021` |
| **`{{BARCODE_6DIGIT}}`** | Zero-padded 6-digit Barcode | Formatted as 6 digits | `009498` |
| **`{{BARCODE_AUTONO}}`** | Internal Auto Sequence Number | Integer | `9498` |
| **`{{NOS}}`** | Stock / Item Nos | Integer | `9498` |
| **`{{VOU_NO}}`** | Voucher Number | String | `9` |

---

### C. Product Details

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{ITEM_NAME}}`** | Product / Item Name | Clean trimmed string | `PAYAL CB`, `RING` |
| **`{{ITEM}}`** | Product Name (alias) | Clean trimmed string | `PAYAL CB` |
| **`{{ITEM_CODE}}`** | Item Short Code | String | `PAY` |
| **`{{PURITY}}`** | Gold / Silver Purity | Clean string | `925`, `22KT`, `18KT`, `916` |
| **`{{HUID}}`** | Hallmark Unique ID (Description 2) | Clean alphanumeric string | `H12345` |
| **`{{DESCRIPTION}}`** | Primary Description | Clean text | `CHAMPALAL`, `ANTIQUE` |
| **`{{DESC}}`** | Description (alias) | Clean text | `ANTIQUE` |
| **`{{SIZE}}`** | Item Ring/Bangle Size | String (`-` if blank) | `14`, `2.4`, `18` |
| **`{{QTY}}`** | Quantity of Pieces | Integer (`0`) | `1` |
| **`{{TYPE}}`** | Type (`PC` or `WT`) | Upper string | `WT` or `PC` |
| **`{{BTYPE}}`** | Type (alias) | Upper string | `WT` or `PC` |
| **`{{TYPE_PC_WT}}`** | Type (alias) | Upper string | `WT` or `PC` |

---

### D. Making Charges & Stone Details

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{MC}}`** | **Making Charge** (rounded up without decimals) | Integer rounded up (Ceiling) | **`450`** *(for 450.00)*, **`451`** *(for 450.25)* |
| **`{{MAKING}}`** | Making Charge (alias, rounded up without decimals) | Integer rounded up (Ceiling) | **`450`** *(for 450.00)*, **`451`** *(for 450.25)* |
| **`{{MC_ROUND}}`** | Making Charge rounded up | Integer rounded up | **`450`** |
| **`{{MAKING_ROUND}}`** | Making Charge rounded up | Integer rounded up | **`450`** |
| **`{{MC_RAW}}`** | Raw Making Charge with decimals | 2 decimal places (`0.00`) | **`450.00`** |
| **`{{MAKING_RAW}}`** | Raw Making Charge with decimals | 2 decimal places (`0.00`) | **`450.00`** |
| **`{{MAKING_TYPE}}`** | Making Charge Basis | String | `GRAM`, `PC`, `%` |
| **`{{MC_TYPE}}`** | Making Charge Basis (alias) | String | `GRAM` |
| **`{{M_PER_GR}}`** | Making Charge Per Gram | 2 decimal places (`0.00`) | `450.00` |
| **`{{STONE_VALUE}}`**| Stone Value / Diamond Value | Integer / Currency (`0`) | `1200` |
| **`{{ST_VAL}}`** | Stone Value (alias) | Integer / Currency (`0`) | `1200` |

---

### E. Costing, Rate & Retail Pricing

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{MRP}}`** | Retail Selling MRP | Formatted without decimals (`0`) | `1560` |
| **`{{PRICE}}`** | MRP Price (alias) | Formatted without decimals (`0`) | `1560` |
| **`{{COST_MC}}`** | Cost Making Charge (rounded up without decimals) | Integer rounded up (Ceiling) | **`250`** *(for 250.00)*, **`251`** *(for 250.25)* |
| **`{{COST_MAKING}}`**| Cost Making (alias, rounded up without decimals) | Integer rounded up (Ceiling) | **`250`** |
| **`{{COST_MC_RAW}}`** | Raw Cost Making with decimals | 2 decimal places (`0.00`) | **`250.00`** |
| **`{{COST_MAKING_RAW}}`**| Raw Cost Making with decimals | 2 decimal places (`0.00`) | **`250.00`** |
| **`{{COST_PRICE}}`** | Calculated Cost Price | 2 decimal places (`0.00`) | `950.00` |
| **`{{CP}}`** | Cost Price (short alias) | 2 decimal places (`0.00`) | `950.00` |
| **`{{COST_RATE}}`** | Cost Rate | 2 decimal places (`0.00`) | `7200.00` |
| **`{{COST_TOUNCH}}`**| Metal Cost Purity / Tounch % | 2 decimal places (`0.00`) | `92.50` |
| **`{{TOUNCH}}`** | Cost Tounch (alias) | 2 decimal places (`0.00`) | `92.50` |
| **`{{TOUCH}}`** | Cost Tounch (alias) | 2 decimal places (`0.00`) | `92.50` |
| **`{{COST_PG}}`** | Cost Per Gram | 2 decimal places (`0.00`) | `7150.00` |
| **`{{COST_PER_GRAM}}`**| Cost Per Gram (alias) | 2 decimal places (`0.00`) | `7150.00` |
| **`{{PROFIT}}`** | Profit / Margin Multiplier | 2 decimal places (`0.00`) | `1.65` |
| **`{{MULTIPLIER}}`** | Margin Multiplier (alias) | 2 decimal places (`0.00`) | `1.65` |
| **`{{MARGIN_MULTIPLIER}}`** | Margin Multiplier (alias) | 2 decimal places (`0.00`) | `1.65` |

---

### F. Supplier, Dates & Metadata

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{SUPPLIER}}`** | **Supplier Party Code** (vendor code, not full name) | Short code string | `RJ01`, `OPN` |
| **`{{SUPPLIER_NAME}}`**| **Supplier Party Code** (alias for `{{SUPPLIER}}`) | Short code string | `RJ01`, `OPN` |
| **`{{PARTY_CODE}}`** | Supplier Party Code (explicit alias) | Short code string | `RJ01`, `OPN` |
| **`{{PARTY}}`** | Supplier Party Code (short alias) | Short code string | `RJ01`, `OPN` |
| **`{{SUPPLIER_FULL_NAME}}`**| Full registered name of supplier | Clean string | `RAJESH JEWELS`, `OPN` |
| **`{{SUPP_INV_NO}}`**| Supplier Invoice / Bill Number | Clean string | `INV-2026-08` |
| **`{{INVOICE_NO}}`** | Supplier Invoice (alias) | Clean string | `INV-2026-08` |
| **`{{SUPP_DATE}}`** | Supplier Invoice Date | `dd/MM/yyyy` | `11/09/2026` |
| **`{{INVOICE_DATE}}`**| Supplier Date (alias) | `dd/MM/yyyy` | `11/09/2026` |
| **`{{P_DATE}}`** | Purchase Date | `dd/MM/yyyy` | `11/09/2026` |
| **`{{PURCHASE_DATE}}`**| Purchase Date (alias) | `dd/MM/yyyy` | `11/09/2026` |
| **`{{PUNCHED_ON}}`** | Entry Timestamp | `dd/MM/yyyy HH:mm` | `11/09/2026 16:30` |
| **`{{DATE}}`** | Current Print Date (Today) | `dd/MM/yyyy` | `11/09/2026` |
| **`{{BOX_NO}}`** | Storage Box Number | Clean string | `BOX-04` |
| **`{{BOX}}`** | Box Number (alias) | Clean string | `BOX-04` |
| **`{{GODOWN}}`** | Store Location / Godown | Clean string | `MAIN` |
| **`{{BRAND}}`** | Brand Name | Clean string | `TATTVA` |
| **`{{REMARKS}}`** | Remarks Line 1 | Clean text | `CUSTOM DESIGN` |
| **`{{NOTE}}`** | Additional Note | Clean text | `GIFT BOX INCLUDED` |
| **`{{STATUS}}`** | Barcode Status | Clean string | `Active` |
| **`{{ENTRY_USER}}`** | Username who created entry | Clean string | `admin` |
| **`{{STICKER_FORMAT}}`**| Selected Sticker Format Name | Clean string | `StickerWt`, `StickerPC` |

---

### G. Store & Company Branding

| Placeholder Tag | Description | Output Format | Example Value |
| :--- | :--- | :--- | :--- |
| **`{{COMPANY_NAME}}`**| Company / Store Brand Name | Clean string | `TATTVA JEWELS` |
| **`{{STORE_NAME}}`** | Store Name (alias) | Clean string | `TATTVA JEWELS` |
| **`{{FIRM_NAME}}`** | Firm Name (alias) | Clean string | `TATTVA JEWELS` |

---

## 4. ZPL Commands & Font Reference for 203 DPI

### A. Font Command Syntax: `^A{font}{orientation},{height},{width}`
- **Font `0` (`^A0N,H,W`)**: Smooth scalable vector font (Arial/Helvetica style).
  - Use for titles, item names, prices, company names.
  - Recommended size: `^A0N,20,20` or `^A0N,22,22`.
  - For bolder/wider punch: set width larger than height, e.g. `^A0N,20,24`.
- **Font `D` (`^ADN,H,W`)**: Razor-sharp bitmap font.
  - Best for small jewelry text like weights, purity, making charges at 203 DPI.
  - Recommended size: `^ADN,18,10` or `^ADN,16,10`.

### B. Block Alignment: `^FB{width},{max_lines},{line_spacing},{align},{indent}`
- `L` = Left aligned (e.g. `^FB200,1,0,L,0`)
- `C` = Centered (e.g. `^FB200,1,0,C,0`)
- `R` = Right aligned (e.g. `^FB200,1,0,R,0`)

### C. Barcode Command: `^BCN,{height},{print_text},{above},{check_digit}`
- Standard Code 128 barcode: `^BY1,2,42^BCN,42,N,N,N^FD{{BARCODE}}^FS`
  - `^BY1,2,42`: Narrow bar width 1 dot, wide/narrow ratio 2, height 42 dots.

### D. Darkness Setting: `~SD{darkness}`
- Values `~SD15` to `~SD25` adjust the thermal head burn darkness for richer black text.

---

## 5. Ready-To-Use Example Templates

### `StickerWt.prn` (For Weight-based items)
```zpl
^XA
^CI28
^PW440
^LL96
^LH0,0
^FO10,6^A0N,20,20^FB200,1,0,L,0^FD{{ITEM_NAME}}^FS
^FO10,28^A0N,18,18^FB200,1,0,L,0^FDNt Wt: {{NET_WT}}g^FS
^FO10,50^A0N,16,16^FB200,1,0,L,0^FD{{PURITY}} {{DESCRIPTION}}^FS
^FO10,72^A0N,16,16^FB200,1,0,L,0^FDMC: {{MAKING}} {{MAKING_TYPE}}^FS
^FO230,6^A0N,18,18^FB200,1,0,C,0^FD{{COMPANY_NAME}}^FS
^FO240,26^BY1,2,42^BCN,42,N,N,N^FD{{BARCODE}}^FS
^FO230,74^A0N,16,16^FB200,1,0,C,0^FD{{BARCODE}}^FS
^XZ
```

### `StickerPC.prn` (For Piece-based items)
```zpl
^XA
^CI28
^PW440
^LL96
^LH0,0
^FO10,8^A0N,22,22^FB200,1,0,L,0^FD{{ITEM_NAME}} {{PURITY}}^FS
^FO10,34^A0N,24,24^FB200,1,0,L,0^FDMRP: {{MRP}}^FS
^FO10,66^A0N,18,18^FB200,1,0,L,0^FDSZ:{{SIZE}} Q:{{QTY}} PC^FS
^FO230,6^A0N,18,18^FB200,1,0,C,0^FD{{COMPANY_NAME}}^FS
^FO240,26^BY1,2,42^BCN,42,N,N,N^FD{{BARCODE}}^FS
^FO230,74^A0N,16,16^FB200,1,0,C,0^FD{{BARCODE}}^FS
^XZ
```
