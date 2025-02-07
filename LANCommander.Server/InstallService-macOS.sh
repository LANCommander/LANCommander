#!/bin/bash
PLIST_FILE=/Library/LaunchDaemons/app.lancommander.server.service.plist
EXEC_PATH=$(pwd)

SERVICE_USER=$USER
read -e -i "$SERVICE_USER" -p "Run as user: " input
SERVICE_USER="${input:-$SERVICE_USER}"

SERVICE_GROUP=$SERVICE_USER
read -e -i "$SERVICE_GROUP" -p "Run in group: " input
SERVICE_GROUP="${input:-$SERVICE_GROUP}"

if [ -f "$PLIST_FILE" ]; then
    echo "Service already exists. Stopping and removing it."
    sudo launchctl unload $PLIST_FILE
    sudo rm -f $PLIST_FILE
fi

echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
<plist version=\"1.0\">
    <dict>
        <key>Label</key>
        <string>com.myapp.service</string>
        <key>ProgramArguments</key>
        <array>
            <string>$EXEC_PATH</string>
        </array>
        <key>RunAtLoad</key>
        <true/>
        <key>KeepAlive</key>
        <true/>
        <key>UserName</key>
        <string>$SERVICE_USER</string>
        <key>GroupName</key>
        <string>$SERVICE_GROUP</string>
    </dict>
</plist>" | sudo tee $PLIST_FILE > /dev/null

sudo launchctl load $PLIST_FILE
sudo launchctl start app.lancommander.server.service

echo "Service 'app.lancommander.server.service' installed and started successfully."