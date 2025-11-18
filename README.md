# Hardline Dreams - Matrix Online Server Emulator

**A modernized .NET 8.0 implementation of the Matrix Online server emulator**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-Check%20repo-blue)](LICENSE)

---

## About This Project

This is a **modernized fork** of the [Hardline Dreams](https://github.com/hdneo/mxo-hd) Matrix Online server emulator, updated for current development practices and cross-platform compatibility.

**What is Matrix Online?** The Matrix Online (MxO) was an MMORPG set in the Matrix universe that ran from 2005-2009. This server emulator aims to preserve and recreate the game experience.

### Why This Fork?

The original Hardline Dreams project laid excellent groundwork but hasn't been actively maintained since ~2020. This fork brings the codebase into 2024+ with:

- ✅ **.NET 8.0 LTS** - Upgraded from .NET Core 3.1 (now EOL)
- ✅ **Cross-platform** - Build and run on macOS, Linux, and Windows
- ✅ **Docker support** - One-command MySQL database setup
- ✅ **Modern tooling** - Works with Rider, VS Code, Visual Studio 2022+
- ✅ **CLI workflow** - Use standard `dotnet` commands
- ✅ **Better documentation** - Comprehensive setup guides for all platforms

## Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- MySQL 8.0 (or use Docker - see below)
- Matrix Online client (CR2 or CR1)

### Option 1: Docker Setup (Recommended for macOS/Linux)

```bash
# Clone the repository
git clone https://github.com/kylebrussell/mxo-hd.git
cd mxo-hd

# Start MySQL with Docker Compose
docker-compose up -d

# Configure the server
cp Config.xml.dist Config.xml

# Build and run
dotnet restore
dotnet build
dotnet run --project "hds/Hardline Dreams MxO server.csproj"
```

**See the detailed [macOS Setup Guide](docs/SETUP-macOS.md) for complete instructions.**

### Option 2: Manual Setup

**1. Set up MySQL:**
```bash
mysql -u root -p
CREATE DATABASE reality_hd CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'mxo'@'localhost' IDENTIFIED BY 'mxopassword';
GRANT ALL PRIVILEGES ON reality_hd.* TO 'mxo'@'localhost';
FLUSH PRIVILEGES;
EXIT;

mysql -u mxo -p reality_hd < SQL/reality_hd.sql
```

**2. Configure and build:**
```bash
cp Config.xml.dist Config.xml
# Edit Config.xml with your database settings

dotnet restore
dotnet build
```

**3. Extract data files:**
```bash
# Extract docs/data.zip to hds/bin/Debug/net8.0/data/
unzip docs/data.zip -d hds/bin/Debug/net8.0/data/
cp Config.xml hds/bin/Debug/net8.0/
```

**4. Run the server:**
```bash
dotnet run --project "hds/Hardline Dreams MxO server.csproj"
```

**Success!** When you see `"I am running :D"`, the server is ready.

## Current Features & State

This emulator is a **work-in-progress** and implements partial functionality of the original Matrix Online game.

### Implemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| **Authentication** | ✅ Working | Login server functional |
| **Character Creation/Selection** | ✅ Working | Margin server operational |
| **Multi-player Support** | ⚠️ Partial | Players can see each other, chat works |
| **Hardlines (Teleportation)** | ✅ Working | Teleporting between locations works well |
| **Movement & Navigation** | ✅ Working | Basic player movement |
| **Inventory System** | ⚠️ Partial | Can equip items, stackables need work |
| **Ability System** | ⚠️ Partial | Loadout changes work, some bugs with multiple abilities |
| **Vendors** | ⚠️ Partial | Buying works (doesn't decrease money), selling not implemented |
| **NPCs/Mobs** | ⚠️ Partial | Spawn and move, combat is very buggy |
| **Doors & Static Objects** | ⚠️ Partial | Can open doors (requires data files) |
| **Mission System** | 🚧 Early stage | Basic framework only |
| **Crews & Factions** | 🚧 Early stage | Creation works, management incomplete |
| **Hyperjump** | ⚠️ Needs work | Implemented but height calculations are off |
| **Friend List** | ⚠️ Untested | Code exists, needs testing |

### Technical Architecture

**Server Components:**
- **Auth Server** (Port 10000) - Handles login authentication
- **Margin Server** (Port 10001) - Character selection and creation
- **World Server** (Port 10002) - Main game world

**Key Features:**
- Entity/View system with internal entity IDs for static and dynamic objects
- Message queue system for RPC and object-related messages with resend until ACK
- Encryption interface for flexibility (currently using EnigmaLib)
- Support for CR1 and CR2 client versions

## In-Game Commands

Once connected to the server, you can use these admin commands (prefix with `?`):

| Command | Description | Example |
|---------|-------------|---------|
| `?org <OrgId>` | Change organization/alignment (0-3) | `?org 1` |
| `?rep <OrgName> <Amount>` | Set faction reputation | `?rep zion 120` |
| `?gotopos X Y Z` | Teleport to coordinates | `?gotopos 100 50 200` |
| `?spawngameobject <GoId>` | Spawn a game object at your location | `?spawngameobject 1234` |
| `?rsi <part> <value>` | Change RSI appearance parts | Use with caution |
| `?moa <hexMoa>` | Change MOA (animation state) | Only visible to you |
| `?playanim <animId>` | Play an animation | `?playanim 42` |
| `?spawndatanode` | Spawn a data node | `?spawndatanode` |

## Development

### IDEs & Tools

- **JetBrains Rider** - Excellent C# IDE with full cross-platform support
- **Visual Studio Code** - With C# Dev Kit extension
- **Visual Studio 2022** - Windows/Mac

### Project Structure

```
mxo-hd/
├── hds/                          # Main server source
│   ├── auth/                     # Authentication server
│   ├── margin/                   # Character selection server
│   ├── world/                    # Game world server
│   ├── databases/                # Database access layer
│   └── ...
├── SQL/                          # Database schema
├── docs/                         # Documentation
│   ├── SETUP-macOS.md           # macOS setup guide
│   ├── SETUP.MD                 # General setup
│   └── data.zip                 # Game object data
├── docker-compose.yml           # MySQL Docker setup
└── Config.xml.dist              # Configuration template
```

### Building for Different Configurations

```bash
# Debug build (default)
dotnet build

# Release build
dotnet build -c Release

# Clean build
dotnet clean && dotnet build

# Run tests (if any)
dotnet test
```

### Contributing

Contributions are welcome! This is a preservation and modernization effort. Areas that need work:

- **Combat system** - NPC combat is very buggy
- **Mission system** - Needs major development
- **Vendor system** - Selling items, proper money handling
- **Testing** - Many features are untested
- **Documentation** - Always can be improved
- **Client compatibility** - Both CR1 and CR2 client support

**To contribute:**
1. Fork this repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## Client Setup

You'll need a Matrix Online client to connect. Detailed client configuration is beyond this README's scope, but generally:

1. **Edit your hosts file** to point MxO domains to `127.0.0.1`
2. **Configure useropts.cfg** with your server settings
3. Ensure you have a **compatible client** (CR2 or CR1)

See community resources at [mxoemu.com](http://www.mxoemu.com) for client setup guides.

## About the Data Files

Static game objects (doors, vendors, NPCs, etc.) are stored in CSV files extracted from `docs/data.zip`. These were parsed using a tool called **Cortana** (created before Windows 10 commandeered the name!).

**Note:** Some object positions may be incorrect due to parsing bugs. The parser is available at: https://github.com/hdneo/cortana-python

## Known Issues

- **Mob combat** is extremely buggy and mostly non-functional
- **Stackable items** in inventory don't work properly
- **Hyperjump height calculations** are off
- **Vendor selling** is not implemented
- **Money transactions** don't properly deduct from player balance
- **Some door positions** are incorrect (parsing artifacts)
- **CR1 client support** uses a workaround with namespaces (needs refactoring)

## Credits & Acknowledgments

### Original Hardline Dreams Project
- **Original Repository:** https://github.com/hdneo/mxo-hd
- **Community Website:** http://www.mxoemu.com

### Special Thanks
- **Rajkosto** (mxoemu.info) - Foundational work on encryption, GoProps, and core server logic
- **Morpheus** (Hardline Dreams) - Research, packet analysis, and the Cortana parsing tool
- **Draxxx** - Provided working client files (2014)
- **MxOSource** - Mission XML files and resources
- **The MxO Community** - Ongoing support, motivation, and packet research

### This Modernization Fork
- Upgraded to .NET 8.0 and modern tooling
- Added Docker support and cross-platform documentation
- Updated package dependencies and fixed breaking API changes
- Cleaned up build artifacts and improved project structure

## License

Please check the repository for license information. This is a fan preservation project for a discontinued game.

## Resources

- **This Fork:** https://github.com/kylebrussell/mxo-hd
- **Original Project:** https://github.com/hdneo/mxo-hd
- **Community:** http://www.mxoemu.com
- **Cortana Tool:** https://github.com/hdneo/cortana-python
- **.NET 8.0:** https://dotnet.microsoft.com/download/dotnet/8.0

---

**Welcome back to The Matrix.** 🟢💊
