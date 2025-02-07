#!/bin/bash
SERVICE_NAME=lancommander.server
SERVICE_FILE=/etc/systemd/system/$SERVICE_NAME.service
EXEC_PATH=$(pwd)

SERVICE_USER=$USER
read -e -i "$SERVICE_USER" -p "Run as user: " input
SERVICE_USER="${input:-$SERVICE_USER}"

SERVICE_GROUP=$SERVICE_USER
read -e -i "$SERVICE_GROUP" -p "Run in group: " input
SERVICE_GROUP="${input:-$SERVICE_GROUP}"

if [ -f "$SERVICE_FILE" ]; then
    echo "Service already exists. Stopping and removing it."
    sudo systemctl stop $SERVICE_NAME
    sudo systemctl disable $SERVICE_NAME
    sudo rm -f $SERVICE_FILE
fi

echo "[Unit]
Description=LANCommander Server
After=network.target

[Service]
ExecStart=$EXEC_PATH
Restart=always
User=$SERVICE_USER
Group=$SERVICE_GROUP
WorkingDirectory=$(dirname $EXEC_PATH)

[Install]
WantedBy=multi-user.target" | sudo tee $SERVICE_FILE > /dev/null

sudo systemctl daemon-reload
sudo systemctl enable $SERVICE_NAME
sudo systemctl start $SERVICE_NAME

echo "Service '$SERVICE_NAME' installed and started successfully."