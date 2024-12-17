# VrScraper Setup Guide

> **Important Update Notice:**  
> If you are updating from a version < 1.0.15, **rename your database file** from `deovrscraper.db` to `vrscraper.db`.

## 1. Running the Service

### Windows
1. **Download & Extract:**  
   Download the latest release (.zip) and extract it.
2. **Configure Port:**  
   Edit `appsettings.json` to set the `Port`.
3. **Start Service:**  
   Run `vrScraper.exe`.
4. **Access Web UI:**  
   Open `http://[YOUR-IP]:[PORT]` in your browser.
5. **Scrape & Refresh:**  
   Start the scraper and reload the page as needed.
6. **Keep Running:**  
   Leave the application open for continuous service.

### Linux
**(TODO)**

## 2. Connecting a VR Headset

### Recommended: HereSphere
1. **Install HereSphere:**  
   Install from your VR store (e.g., Meta Store).
2. **Connect to VrScraper:**  
   In the HereSphere browser, open:  
   `http://[YOUR-IP]:[PORT]`  
   If you see a default web view, try:  
   `http://[YOUR-IP]:[PORT]/heresphere`

### DeoVR
1. **Install DeoVR:**  
   Install from the headset’s app store (e.g., Oculus Store).
2. **Connect to VrScraper:**  
   In the DeoVR browser, open:  
   `http://[YOUR-IP]:[PORT]`  
   If you see a default web view, try:  
   `http://[YOUR-IP]:[PORT]/deovr`

## 3. Updating VrScraper
1. **Backup:**  
   Save copies of `appsettings.json` and `vrscraper.db`.
2. **Replace Files:**  
   Download the latest release and replace the old files.
3. **Restore Backups:**  
   Copy your backed-up files back into the folder.

## 4. Development
To add a new migration:
```bash
dotnet ef migrations add [MIGRATION_NAME] -o ./DB/Migrations
