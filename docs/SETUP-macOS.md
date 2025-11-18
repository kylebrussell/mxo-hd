# macOS Setup Guide for Hardline Dreams MxO Server

This guide walks you through setting up the Hardline Dreams Matrix Online server emulator on macOS using modern cross-platform tools.

## Prerequisites

### 1. Install .NET SDK 8.0

The project has been modernized to use **.NET 8.0** (LTS).

**Install via Homebrew:**
```bash
brew install --cask dotnet-sdk
```

**Verify installation:**
```bash
dotnet --version
# Should show 8.0.x or higher
```

### 2. Install Docker Desktop (Recommended)

Docker provides the easiest way to run MySQL locally.

**Install via Homebrew:**
```bash
brew install --cask docker
```

**Or download from:** https://www.docker.com/products/docker-desktop

After installation, launch Docker Desktop from Applications.

### 3. Alternative: Install MySQL Directly

If you prefer to install MySQL directly instead of using Docker:

```bash
brew install mysql@8.0
brew services start mysql@8.0
```

## Quick Start with Docker (Recommended)

### 1. Clone the Repository

```bash
git clone https://github.com/hdneo/mxo-hd.git
cd mxo-hd
```

### 2. Start MySQL with Docker Compose

The repository includes a `docker-compose.yml` that automatically:
- Starts MySQL 8.0 with the correct charset (utf8mb4)
- Creates the `reality_hd` database
- Imports the SQL schema from `SQL/reality_hd.sql`
- Sets up a user `mxo` with password `mxopassword`

```bash
docker-compose up -d
```

**Wait for MySQL to initialize:**
```bash
# Check if MySQL is ready
docker-compose logs -f mysql

# You should see: "ready for connections"
# Press Ctrl+C to exit logs
```

### 3. Configure the Server

Copy the example configuration:
```bash
cp Config.xml.dist Config.xml
```

The default `Config.xml` is already configured for the Docker MySQL setup. If you need to customize it, edit the database section:

```xml
<Database>
  <Host>localhost</Host>
  <Port>3306</Port>
  <Username>mxo</Username>
  <Password>mxopassword</Password>
  <Database>reality_hd</Database>
</Database>
```

### 4. Restore Dependencies

```bash
dotnet restore
```

### 5. Build the Project

```bash
# Build using the solution file
dotnet build hds.sln

# Or build using the project file directly
dotnet build "hds/Hardline Dreams MxO server.csproj"
```

**Expected output:**
- The build should complete with warnings but **0 errors**
- Output will be in `hds/bin/Debug/net8.0/`

### 6. Extract Data Files

The server requires static game object data files:

```bash
# Download and extract data.zip from the docs folder
# (Note: You may need to download this separately if it's not in the repo)
unzip docs/data.zip -d hds/bin/Debug/net8.0/data/
```

If `data.zip` is not available, check the [project documentation](../README.md) or releases.

### 7. Copy Config to Output Directory

```bash
cp Config.xml hds/bin/Debug/net8.0/
```

### 8. Run the Server

```bash
# Navigate to the output directory
cd hds/bin/Debug/net8.0/

# Run the server
dotnet hds.dll
```

**Or from the project root:**
```bash
dotnet run --project "hds/Hardline Dreams MxO server.csproj"
```

### 9. Expected Output

The server will initialize and you should see logs indicating:
- Database connection successful
- Loading static objects from CSV files
- Starting Auth, Margin, and World servers
- Final message: **"I am running :D"**

If you see this message, the server is running successfully!

## Manual MySQL Setup (Without Docker)

If you're not using Docker, follow these steps:

### 1. Create the Database

```bash
# Connect to MySQL
mysql -u root -p

# Create database and user
CREATE DATABASE reality_hd CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'mxo'@'localhost' IDENTIFIED BY 'mxopassword';
GRANT ALL PRIVILEGES ON reality_hd.* TO 'mxo'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```

### 2. Import the SQL Schema

```bash
mysql -u mxo -p reality_hd < SQL/reality_hd.sql
# Enter password: mxopassword
```

### 3. Continue with steps 3-9 from Quick Start

## Development Workflow

### Building and Running

```bash
# Clean build
dotnet clean
dotnet build

# Run in development mode
dotnet run --project "hds/Hardline Dreams MxO server.csproj"

# Build for release
dotnet build -c Release
```

### Useful Commands

```bash
# Restore NuGet packages
dotnet restore

# List project dependencies
dotnet list package

# Update packages (if needed)
dotnet add package <PackageName>

# Stop and remove Docker containers
docker-compose down

# View MySQL logs
docker-compose logs -f mysql

# Access MySQL shell in Docker
docker exec -it mxo-hd-mysql mysql -u mxo -p reality_hd
```

### Database Management

**Using Docker MySQL:**
```bash
# Connect to MySQL in Docker
docker exec -it mxo-hd-mysql mysql -u mxo -p reality_hd

# Backup database
docker exec mxo-hd-mysql mysqldump -u mxo -pmxopassword reality_hd > backup.sql

# Restore database
docker exec -i mxo-hd-mysql mysql -u mxo -pmxopassword reality_hd < backup.sql
```

## Troubleshooting

### "Connection refused" to MySQL

**Using Docker:**
```bash
# Check if MySQL container is running
docker-compose ps

# Restart MySQL
docker-compose restart mysql

# Check MySQL logs for errors
docker-compose logs mysql
```

**Using local MySQL:**
```bash
# Check if MySQL is running
brew services list

# Start MySQL
brew services start mysql@8.0
```

### Build Errors

**"The target framework is not installed":**
```bash
# Install .NET 8.0 SDK
brew install --cask dotnet-sdk
```

**Package restore fails:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore again
dotnet restore
```

### "Could not find data files"

Make sure you've extracted `data.zip` to the output directory:
```bash
ls -la hds/bin/Debug/net8.0/data/
# Should show CSV files for game objects
```

### Server Crashes on Startup

Check these common issues:
1. **Database connection:** Verify MySQL is running and credentials are correct
2. **Missing data files:** Ensure `data` folder exists in the output directory
3. **Config.xml:** Verify the file exists and has correct settings
4. **Port conflicts:** Make sure ports 10000, 10001, 10002 are not in use

```bash
# Check what's using a port
lsof -i :10000
lsof -i :10001
lsof -i :10002
```

## IDE Support

### Visual Studio Code
```bash
# Install C# Dev Kit extension
code --install-extension ms-dotnettools.csdevkit

# Open project
code .
```

### JetBrains Rider
Download from: https://www.jetbrains.com/rider/

Open the `hds.sln` solution file.

### Visual Studio for Mac
Open the `hds.sln` solution file.

## Next Steps

Once the server is running:

1. **Configure a Matrix Online client** to connect to your local server
   - Edit your `hosts` file to point Matrix Online domains to `127.0.0.1`
   - Configure `useropts.cfg` with your server settings
   - See the main [README](../README.md) for client setup details

2. **Explore the codebase:**
   - `hds/auth/` - Authentication server
   - `hds/margin/` - Character selection server
   - `hds/world/` - Game world server
   - `hds/databases/` - Database access layer

3. **Check available commands:**
   - See the [README](../README.md) for in-game chat commands
   - Commands start with `?` (e.g., `?org 1`, `?gotopos X Y Z`)

## Additional Resources

- **Main Documentation:** [README.md](../README.md)
- **General Setup:** [docs/SETUP.MD](SETUP.MD)
- **Project Website:** http://www.mxoemu.com
- **GitHub Issues:** https://github.com/hdneo/mxo-hd/issues

## Contributing

If you encounter issues with this guide or want to contribute improvements:

1. Report issues on GitHub
2. Submit pull requests with fixes
3. Update documentation as the project evolves

---

**Happy hacking, and welcome back to The Matrix!** 🟢
