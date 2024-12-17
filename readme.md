# VrScraper Setup Guide

Follow these steps to set up and run the VrScraper on Windows and VR headsets.

## 1) Running on Windows

1. **Download and Extract:**
   - Download the latest release .zip and extract it to a folder.

2. **Configure Settings:**
   - Edit `appsettings.json` to set the Port.

3. **Launch the Application:**
   - Run `vrScraper.exe`.

4. **Access the Interface:**
   - Open your browser and go to `http://[YOUR-IP]:[PORT]`.

5. **Start Scraping:**
   - Run the scraping process and reload as needed.

6. **Keep Running:**
   - Keep the application running for continuous use.

## 2) Running on a VR Headset with HereSphere (recommended)

1. **Install HereSphere:**
   - Install the HereSphere App on your VR headset from the app store (e.g. Occulus Store)

2. **Connect to VrScraper:**
   - Open HereSphere App on your VR device and browse to `http://[YOUR-IP]:[PORT]`
   - If this does show the default webserver view (instead of a nice gallery)
     try this one `http://[YOUR-IP]:[PORT]/heresphere`
   - 
## 3) Running on a VR Headset with DeoVR

1. **Install DeoVr:**
   - Install the DeoVr App on your VR headset from the app store (e.g. Occulus Store)

2. **Connect to VrScraper:**
   - Open DeoVr App on your VR device and browse to `http://[YOUR-IP]:[PORT]`
   - If this does show the default webserver view (instead of a nice gallery)
     try this one `http://[YOUR-IP]:[PORT]/deovr`

## Updating VrScraper

1. **Backup Files:**
   - Backup `appsettings.json` and `deovrscraper.db`.

2. **Download and Replace:**
   - Download the latest release and replace the old files.

3. **Restore Backups:**
   - Restore your backed-up files.

## Development

To add a new migration, run:

```bash
dotnet ef migrations add [##NAME-ME##] -o ./DB/Migrations

