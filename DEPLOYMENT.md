# ResumeCraft — Jetson Orin Nano Deployment Guide

This guide explains how to build and run the entire ResumeCraft application stack (Nginx, .NET 8 API, MongoDB, PostgreSQL) on your Jetson Orin Nano using Docker Compose.

---

## Prerequisites on Jetson Orin Nano

1. **Docker & Docker Compose**: Ensure Docker and the Docker Compose plugin are installed:
   ```bash
   sudo apt update
   sudo apt install -y docker-compose-plugin
   ```
2. **Ollama (Native)**: Ollama must run directly on the host (not in Docker) to have full GPU/CUDA acceleration on the Jetson Orin Nano:
   ```bash
   # Install Ollama
   curl -fsSL https://ollama.com/install.sh | sh
   
   # Download the Gemma 2:2b model
   ollama run gemma2:2b
   ```

---

## Deployment Steps

### Step 1: Build the Angular Frontend (on your Dev Machine)
Before transferring files to the Jetson, compile the frontend assets for production:
1. Open a terminal in `d:\Angular Stuff\resumeBuilderClient\`
2. Run the production build command:
   ```bash
   npm run build
   ```
   *This compiles all files and outputs them to `resumeBuilderClient/dist/resume-builder-client/browser/`.*

### Step 2: Transfer Files to the Jetson Orin Nano
Transfer the backend solution folder (`D:\.NET Stuff\resumeBuilder\`) to the Jetson. 
Ensure the compiled Angular assets are placed inside the `./client-dist` directory next to your `docker-compose.yml` file:

```
[your-deploy-folder]/
├── client-dist/             <-- Copy files from resumeBuilderClient/dist/resume-builder-client/browser/ here
├── nginx/
│   └── default.conf
├── BaseApi.WebApi/
├── Dockerfile
└── docker-compose.yml
```

### Step 3: Run the Stack with Docker Compose (on Jetson)
SSH into your Jetson Orin Nano, navigate to your deploy directory, and boot the containers:

```bash
# Start all services in the background
sudo docker compose up -d --build
```

---

## Verifying the Deployment

1. **Verify Web Interface**: Open a web browser on any device in your network and go to `http://[your-jetson-ip]/` (e.g. `http://192.168.1.123/`). The login page should load.
2. **Database Migration**: The API container automatically runs migrations to create the database schemas on startup.
3. **Verify Ollama Connection**: The API container is configured to talk to your host's native Ollama service via the `host.docker.internal` bridge network gateway on port `11434`.

---

## 📧 How to Configure Real Email Notifications

By default, the app is in **Test Mode** (reset tokens are displayed directly on the screen so you don't need to configure anything to test). 

If you want to configure real email sending:
1. Open `appsettings.Production.json` on the Jetson.
2. Find the `SmtpSettings` section and fill in your details:
   ```json
     "SmtpSettings": {
       "Server": "smtp.gmail.com",
       "Port": 587,
       "SenderName": "ResumeCraft Support",
       "SenderEmail": "yourname@gmail.com",
       "Username": "yourname@gmail.com",
       "Password": "your-16-char-app-password",
       "EnableSsl": true
     }
   ```
3. Restart the containers:
   ```bash
   sudo docker compose restart api
   ```
