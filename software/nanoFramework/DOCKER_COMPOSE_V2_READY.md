# Docker Compose V2 - Ready to Build! ✅

## ✅ Your Setup Verified

**You have Docker Compose V2** - this is the modern, recommended version!

```
Docker version: 29.2.1, build a5c7197
Docker Compose: v5.0.2
```

---

## 🔄 What Changed

### Old Syntax (V1):
```bash
docker-compose run ...   # ❌ Old (hyphenated command)
```

### New Syntax (V2) - What You Have:
```bash
docker compose run ...   # ✅ New (subcommand)
```

**Your build scripts have been updated to use V2 syntax!**

---

## 🚀 How to Build (WSL)

Since Docker is running in WSL, you have **two options**:

### Option 1: Run from WSL (Recommended)

```bash
# In WSL terminal
cd /home/cp/Dev/diseqc_cntrl/software/nanoFramework
chmod +x build.sh
docker compose run --rm nanoframework-build /work/build.sh
```

### Option 2: Run from PowerShell (via WSL)

```powershell
# In PowerShell
cd \\wsl.localhost\Debian\home\cp\Dev\diseqc_cntrl\software\nanoFramework
wsl bash -c "cd /home/cp/Dev/diseqc_cntrl/software/nanoFramework && docker compose run --rm nanoframework-build /work/build.sh"
```

**Recommendation:** Use **Option 1** (WSL terminal) for better compatibility.

---

## 📋 Complete Build Steps (WSL)

Open a **WSL terminal** (not PowerShell):

```bash
# 1. Navigate to project
cd /home/cp/Dev/diseqc_cntrl/software/nanoFramework

# 2. Make build script executable
chmod +x build.sh

# 3. Run build
docker compose run --rm nanoframework-build /work/build.sh

# 4. Wait 10-15 minutes (first build)
# Subsequent builds: 2-5 minutes

# 5. Check output
ls -lh build/nanoCLR.bin
```

---

## ✅ Updated Files

The following files now use Docker Compose V2 syntax:

- ✅ `build.sh` - Updated to `docker compose`
- ✅ `build.ps1` - Updated to `docker compose`
- ✅ `docs/guides/DOCKER_BUILD_GUIDE.md` - Documentation updated

**`docker-compose.yml` filename stays the same!** (This is correct for V2)

---

## 🎯 Quick Commands

```bash
# Build firmware
docker compose run --rm nanoframework-build /work/build.sh

# Pull latest image
docker compose pull

# View running containers
docker compose ps

# Stop all containers
docker compose down

# View logs
docker compose logs
```

---

## 🔧 Troubleshooting

### If you get "docker: command not found"

You're in Windows PowerShell, not WSL. Either:

**A) Switch to WSL terminal:**
```bash
wsl
cd /home/cp/Dev/diseqc_cntrl/software/nanoFramework
```

**B) Call via wsl from PowerShell:**
```powershell
wsl bash -c "cd ~ && docker compose version"
```

### If you get permission denied

```bash
# In WSL
sudo usermod -aG docker $USER
# Log out and back in to WSL
```

---

## ✨ Advantages of Docker Compose V2

1. ✅ **Faster** - Written in Go (not Python)
2. ✅ **Built-in** - Part of Docker CLI
3. ✅ **Better resource handling**
4. ✅ **Improved error messages**
5. ✅ **Active development** - V1 is deprecated

---

## 🚀 You're Ready to Build!

**From WSL terminal:**
```bash
cd /home/cp/Dev/diseqc_cntrl/software/nanoFramework
docker compose run --rm nanoframework-build /work/build.sh
```

**After 10-15 minutes:** You'll have `build/nanoCLR.bin` ready to flash! 🎉

---

**Your build system is using the modern, recommended Docker Compose V2!** ✅

