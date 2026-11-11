First release of Voxl Display: a tablet as a second monitor on Windows 10 and 11.

**What it does**

- Creates a virtual monitor with the signed Virtual Display Driver.
- Streams it to any browser on your LAN with voxl (H.264 over HTTPS).
- One window to set resolution, DPI scaling, framerate, encoder preset, CRF and bitrate.

**Install**

1. Download `VoxlDisplay-1.0.0.zip` and unpack it.
2. Run `install.cmd` and accept the UAC prompt (driver + firewall rule).
3. Open **Voxl Display** from the desktop shortcut and press **Start**.
4. On the tablet: open the shown link, or scan the QR code, accept the self-signed
   certificate once, and sign in. Any user name works; the default password is
   `voxl6901` and can be changed in the app.

Works offline: the driver package and nefcon are bundled in `vendor/`.

**Signed with a self-signed certificate.** Windows reports "Unknown publisher" until
`trust.ps1` is run elevated on that machine.

**Limitations**

- x64 only (no ARM64 build of voxl yet).
- A still desktop sends almost no data; that is intended, the encoder only sends changes.
- The stream pauses while the PC is locked.

Full details, security notes and uninstall steps are in the README inside the zip.
