#!/bin/bash

sudo yum -y update
sudo yum -y install libunwind
sudo apt -y update
pip install awscli --upgrade --user
npm uninstall -g aws-sam-local
rm `which sam`
pip install aws-sam-cli --user
ln -sfn $(which sam) ~/.c9/bin/sam
curl -O https://dot.net/v1/dotnet-install.sh
sudo chmod u=rx dotnet-install.sh
./dotnet-install.sh -c Current
export PATH=$PATH:$HOME/.local/bin:$HOME/bin:$HOME/.dotnet
rm dotnet-install.sh
