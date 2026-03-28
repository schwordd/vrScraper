---
name: stop-app
description: Stop the running vrScraper app
user-invocable: true
---

Find and kill the running vrScraper process:

```bash
taskkill //PID $(tasklist 2>/dev/null | grep -i vrScraper | awk '{print $2}') //F 2>&1
```

If that fails, try:

```bash
taskkill //IM vrScraper.exe //F 2>&1
```

Wait 2 seconds after killing before attempting to build or restart.
