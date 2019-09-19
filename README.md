# UserAppMinder
Web-based task manager for terminal server environment (experimental).

Right now, the performance is abysmal, and it's more of a toy than anything.

Might need to open up the Windows firewall for the account running the UAM service on the server:

netsh http add urlacl url=http://*:25100/ user=<domain>\<service user name>
