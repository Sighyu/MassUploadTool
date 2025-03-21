# MassUploadTool

MassUploadTool is a bulk file uploader for Discord that lets you send multiple attachments to a channel in a single message. The tool supports batching files (up to 10 files per batch) with a configurable maximum batch size (10MB, 50MB, or 500MB), recursive directory scanning, file type filtering, detailed reporting, and robust error handling with a retry mechanism.

## Features

- **Bulk Upload to Discord:**  
  Upload up to 10 attachments per message with a maximum batch size that you can select (10MB, 50MB, or 500MB).

- **Dynamic Configuration:**  
  - Save your Discord token and default allowed file extensions in a configuration file (`appsettings.json`).
  - On first run, you'll be prompted for a Discord token. The tool will then fill in default allowed file extensions and exit so you can update the configuration as needed.

- **Recursive Directory Scanning:**  
  Supports nested directories. Specify multiple directories separated by the `|` character.

- **File Type Filtering:**  
  Only process files with allowed extensions (configurable via `appsettings.json`).

- **Retry Mechanism & Rate Limit Handling:**  
  Automatically retries failed HTTP requests and, if Discord's rate limit is reached, waits 2 minutes before retrying.

- **Detailed Reporting & Logging:**  
  Generates a detailed report (`detailed_report.txt`) and logs all activity using Serilog (logs written to `log.txt`).



## Preview

![](https://i.imgur.com/YioIgOi.png)
