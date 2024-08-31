# DeoVrScraper Setup Guide

Follow these steps to set up and run the DeoVrScraper on Windows and VR headsets.

## 1) Running on Windows

1. **Download and Extract:**
   - Download the latest release and extract it to a folder.

2. **Configure Settings:**
   - Edit `appsettings.json` to set the IP and Port.

3. **Launch the Application:**
   - Run `deovrScraper.exe`.

4. **Access the Interface:**
   - Open your browser and go to `http://[IP]:[PORT]`.

5. **Start Scraping:**
   - Run the scraping process and reload as needed.

6. **Keep Running:**
   - Keep the application running for continuous use.

## 2) Running on a VR Headset

1. **Install DeoVr:**
   - Install the DeoVr App on your VR headset.

2. **Connect to DeoVrScraper:**
   - Open DeoVr and navigate to `http://[IP]:[PORT]`.

## Updating DeoVrScraper

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
