# EWEADN S9 series battery support

## Supported models

The EWEADN S9 family (vendor ID `372E`) exposes a vendor feature collection on `FF00:0001` with a
64-byte feature report. The product IDs come from the device table in EWEADN's own `config.xml`
(S9 software 1.0.1.8):

| Connection | Product ID | Models |
|---|---|---|
| 2.4 GHz receiver | `1026` | S9 Pro, S9 Ultra, S9 Max, S9 Ultra+ |
| 2.4 GHz receiver | `1033` | S9 Pro, S9 Ultra |
| 2.4 GHz receiver | `103D` | S9 Pro, S9 Ultra, S9 Max, S9 Ultra+ (also used by S7 Max / Pro / Ultra) |
| 2.4 GHz receiver | `1055` | S9 Pro, S9 Ultra, S9 Max |
| USB cable | `1029` | S9 Pro |
| USB cable | `1027` | S9 Ultra |
| USB cable | `105E` | S9 Max |
| USB cable | `108D` | S9 Ultra+ |

Hardware-tested on an S9 Pro 8K over the 2.4 GHz receiver (`372E:1026`, receiver firmware v1052).
The other receiver and cable IDs are implemented from the vendor's device table and have not been
tested on hardware.

## Protocol

The battery query is the MousePix `getBasics()` call: `Bull.request(0x4F, 0x81)`, sent as feature
report `03` on `FF00:0001`. Requests are 64 bytes; EWEADN uses sequence number 0:

```text
03 20 50 00 00 4F 81 00 ...
```

| Byte | Meaning |
|---|---|
| 0 | Report ID `03` |
| 1 | Checksum: sum of bytes 2..63, low byte (`20`) |
| 2 | Marker `50` |
| 3 | `00` |
| 4 | Sequence number (`00` for EWEADN) |
| 5 | Command `4F` |
| 6 | Page `81` (`getBasics`) |

The older vendor `DeviceDriver.exe` 1.0.1.8 sent the same query with sequence number `2`:
`03 22 50 00 02 4F 81 ...`. The receiver echoes the sequence number in its reply, so both work.

Reply (captured from the S9 Pro 8K, sequence 2):

```text
03 50 00 0B 02 01 5E 21 07 01 90 65 04 A5 5A 00 ...
```

| Byte | Meaning |
|---|---|
| 0 | Report ID `03` |
| 1 | Marker `50` |
| 2 | Acknowledgement: `00` = OK |
| 3 | Payload length (`0B` = 11 bytes) |
| 4 | Sequence number echo (`02` in the capture, `00` for the driver's request) |
| 5 | `01` in the `getBasics` reply |
| 6 | First payload byte: bit 7 = charging, bits 0..6 = percent (`5E` = 94) |

The driver rejects a reply with a non-zero acknowledgement, a different sequence number, an empty
or oversized payload, or a level above 100.

The receiver is also asked whether the mouse is linked, with command `47`, page `00`:

```text
03 97 50 00 00 47 00 00 ...
```

A payload byte of `01` means the mouse is linked; `00` is reported as "Connected" with the last
known level instead of asking for a battery reading.

## Source

- `config.xml` in EWEADN's S9 software maps every receiver and cable product ID above to the S9
  models.
- EWEADN's current E HUB web driver (`new-hub.eweadn.cn`) uses the Beiying/MousePix provider for
  these devices. `getBasics()` in `@bytech/mouse-pix` 1.1.1 calls `Bull.request(0x4F, 0x81)`, and
  reads the battery from the first payload byte.
- The request and reply above were captured from the S9 Pro 8K receiver and match the SDK parser.

The old `0F/06` info page is answered with status `02` by receiver firmware v1052 and is not used.

## Notes

- **Read-only.** The driver sends only the link check and `getBasics` queries. It never writes
  settings, DPI, lighting or firmware.
- **Close E HUB while polling.** E HUB also uses feature report 3. If a poll is lost while E HUB
  owns the collection, the driver retries three times and then shows `Connected` until the next
  refresh.
- **Display smoothing.** The receiver reports raw 1% steps, and this driver shows them unchanged.
  E HUB smooths its own display, so it can show 94% while the raw reply byte is `0x5D` = 93%.
- **Polling.** The app polls every 60 seconds by default; the interval is configurable from the
  menu. A poll wakes the receiver, so a shorter interval costs a little more power.
