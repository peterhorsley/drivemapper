### Configure and manage mapped network drives on Windows

If you are frustrated by Windows not properly reconnecting your mapped network drives, this tool may help you.

Usage: DriveMapper.exe [map] [add] [delete]

* Type `drivemapper add` to add a mapped drive to the program's config.
* Type `drivemapper delete` to remove a drive from the program's config.
* Type `drivemapper map` to perform the net use command to map all configured drives.

- Passwords are stored securely in Windows Credential Manager.
- The program intentionally configures drive mappings to not be persistent to ensure on reboot/login you have no half-mapped drives.

