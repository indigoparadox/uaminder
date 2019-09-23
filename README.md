# Obsolete
With the release of the magnificent [Windows Admin Center](https://docs.microsoft.com/en-us/windows-server/manage/windows-admin-center/understand/windows-admin-center), this is pretty much obsolete. Leaving it here for posterity/potential future ideas that could come from it.

# UserAppMinder
Web-based task manager for terminal server environment (experimental).

Right now, the performance is abysmal, and it's more of a toy than anything.

Might need to open up the Windows firewall for the account running the UAM service on the server:

netsh http add urlacl url=http://*:25100/ user=<domain>\<service user name>
