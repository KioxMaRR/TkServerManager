# TkServerManager
TK Server - TCP Control & Whitelist Manager
===========================================

📌 Overview:
------------
This is a TCP server written in C# that:
- Manages client connections on port 5000.
- Uses an authentication system based on SteamID and Serial.
- Stores and manages the list of whitelisted users in files.
- Can send the mods list, launcher download link, and connected client count to clients.
- Provides a special remote PowerShell shell access via a secure command.
- Has an automatic game server restart scheduler using batch scripts.
- When the console is closed, it trims the whitelist.txt file to keep only the first 6 lines.

📂 Files and Purpose:
---------------------
- `whitelist.json` → Stores full list of users (SteamID + Serial + Name)
- `whitelist.txt` → List of allowed SteamIDs (for the game server)
- `modslist.txt` → Active mods list (semicolon `;` separated)
- `LinkLauncher.txt` → Launcher download link
- `stopserver.bat` / `startserver.bat` → Scripts to stop and start the game server
- `README.txt` → This documentation

💻 Server Console Commands:
----------------------------
(Type in the server console while it is running)

- `help` → Show available commands
- `clients` → Show the number of connected clients
- `users` → List registered users from whitelist.json
- `clear` → Clear the console
- `exit` → Shut down the server (will clean whitelist.txt before exit)
- `setmods` → Set the server mod list (separated by `;`)
- `setupdate` → Save launcher link to LinkLauncher.txt
- `setrestart HH:mm` → Schedule periodic restart (example: `setrestart 01:30` = every 1h 30m)
- `cancelrestart` → Cancel scheduled restart
- `nextrestart` → Show the next scheduled restart time

🔌 Client Protocol & Messages:
------------------------------
After connecting, clients must first send their version:
Server responds:
- `VERSION_OK` → Version matches
- `UPDATE_REQUIRED` → Version is outdated

Client commands to the server:
- `GET_Link_Launcher` → Get launcher link from LinkLauncher.txt
- `REQUEST_SHELL_[TK]KIOXMAR` → Request remote PowerShell shell access (requires password)
- `GET_COUNT` → Get the number of connected clients
- `GET_MODS` → Get the server mods list
- `{SteamID}|{Serial}|{Name}` → Register or log in the user

🔑 Remote Shell Access:
-----------------------
- Only accessible with the fixed password `Z@5g!7pL*mXv2#eFKR`
- After authentication, allows executing PowerShell commands directly on the server.
- Exit shell mode by typing `exit`

⚠ Security Notes:
-----------------
- This server allows remote command execution on the host machine. **Strongly recommended** to use only in a secure environment with trusted clients.
- For public use, change the password and port, and implement TLS encryption.
