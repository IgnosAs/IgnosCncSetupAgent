# IgnosCncSetupAgent
Ignos CncSetup local agent service for file transfer to and from CNC machines

# Installation
sc.exe create "Ignos CNC Setup Agent Service" start=auto binpath="path.to.agent.exe" obj="NT AUTHORITY\LocalService"

(If desirable, and local computer has access to shares, run as obj="NT AUTHORITY\NetworkService")
Give access to folder containing service files to account running the service
