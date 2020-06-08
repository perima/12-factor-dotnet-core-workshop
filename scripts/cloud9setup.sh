#!/bin/bash

sudo yum -y update
sudo yum -y install libunwind
sudo apt -y update
pip install awscli --upgrade --user
pip install aws-sam-cli --upgrade --user
ln -sfn $(which sam) ~/.c9/bin/sam
curl -s -L https://dot.net/v1/dotnet-install.sh -O
sudo chmod u=rx dotnet-install.sh
./dotnet-install.sh -c Current
export PATH=$PATH:$HOME/.local/bin:$HOME/bin:$HOME/.dotnet
rm dotnet-install.sh
