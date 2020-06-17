#!sh

echo "Setup - Part 1"

git config --global credential.helper '!aws codecommit credential-helper $@'
git config --global credential.UseHttpPath true
git config --global user.name "{{cookiecutter.your_name}}"
git config --global user.email {{cookiecutter.your_email_address}}

aws s3 mb s3://{{cookiecutter.codeupload_bucket_name}}  --region {{cookiecutter.AWS_region}} 
aws secretsmanager create-secret --name "/Prod/{{cookiecutter.project_name}}/Metadata" --description "The regular expression to use to determine if detected text is a valid number plate" --secret-string "{\"NumberPlateRegEx\":\"{{cookiecutter.numberplate_regex}}\",\"Somekey\":\"SomeValue\"}" --region {{cookiecutter.AWS_region}} 
aws ssm put-parameter --name "/Prod/{{cookiecutter.project_name}}/TollgateCharge" --description "The charge for a vehicle to use the toll gate" --value "5" --type "String"  --region {{cookiecutter.AWS_region}} 
aws secretsmanager create-secret --name "/Staging/{{cookiecutter.project_name}}/Metadata" --description "The regular expression to use to determine if detected text is a valid number plate" --secret-string "{\"NumberPlateRegEx\":\"{{cookiecutter.numberplate_regex}}\",\"Somekey\":\"SomeValue\"}" --region {{cookiecutter.AWS_region}} 
aws ssm put-parameter --name "/Staging/{{cookiecutter.project_name}}/TollgateCharge" --description "The charge for a vehicle to use the toll gate" --value "5" --type "String"  --region {{cookiecutter.AWS_region}} 

cd ~/environment/{{cookiecutter.project_name}}/repos/Baseline-Staging
sam package --template-file template.yml --s3-bucket {{cookiecutter.codeupload_bucket_name}} --output-template-file template-export.yml --region {{cookiecutter.AWS_region}}
sam deploy  --template-file template-export.yml --stack-name {{cookiecutter.project_name_baseline}}-Staging --region {{cookiecutter.AWS_region}} --capabilities CAPABILITY_IAM
cd ~/environment/{{cookiecutter.project_name}}/repos/Baseline-Prod
sam package --template-file template.yml --s3-bucket {{cookiecutter.codeupload_bucket_name}} --output-template-file template-export.yml --region {{cookiecutter.AWS_region}}
sam deploy  --template-file template-export.yml --stack-name {{cookiecutter.project_name_baseline}}-Prod --region {{cookiecutter.AWS_region}} --capabilities CAPABILITY_IAM

aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Staging --item "{\"numberPlate\": {\"S\": \"SSS650\"},\"credit\": {\"N\": \"12.50\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"John\"},\"ownerLastName\": {\"S\": \"Smith\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Staging --item "{\"numberPlate\": {\"S\": \"PCF606\"},\"credit\": {\"N\": \"3.90\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"Jane\"},\"ownerLastName\": {\"S\": \"Doe\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Staging --item "{\"numberPlate\": {\"S\": \"SOB640\"},\"credit\": {\"N\": \"21.70\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"Bruce\"},\"ownerLastName\": {\"S\": \"Wayne\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Staging --item "{\"numberPlate\": {\"S\": \"TESTPLATE\"},\"credit\": {\"N\": \"29.10\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"Frank\"},\"ownerLastName\": {\"S\": \"Furt\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Prod --item "{\"numberPlate\": {\"S\": \"SSS650\"},\"credit\": {\"N\": \"12.50\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"John\"},\"ownerLastName\": {\"S\": \"Smith\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Prod --item "{\"numberPlate\": {\"S\": \"PCF606\"},\"credit\": {\"N\": \"3.90\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"Jane\"},\"ownerLastName\": {\"S\": \"Doe\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Prod --item "{\"numberPlate\": {\"S\": \"SOB640\"},\"credit\": {\"N\": \"21.70\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"Bruce\"},\"ownerLastName\": {\"S\": \"Wayne\"}}";
aws dynamodb put-item --region {{cookiecutter.AWS_region}} --table-name {{cookiecutter.dynamodb_tablename}}-Prod --item "{\"numberPlate\": {\"S\": \"TESTPLATE\"},\"credit\": {\"N\": \"29.10\"},\"ownerEmail\": {\"S\": \"{{cookiecutter.your_email_address}}\"},\"ownerFirstName\": {\"S\": \"Frank\"},\"ownerLastName\": {\"S\": \"Furt\"}}";

cd ~/environment/{{cookiecutter.project_name}}/repos/Process
sam package --template-file pipeline.yml --s3-bucket {{cookiecutter.codeupload_bucket_name}} --output-template-file pipeline-export.yml --region {{cookiecutter.AWS_region}}
sam deploy  --template-file pipeline-export.yml --stack-name {{cookiecutter.project_name_process}}-CICD-Pipeline --region {{cookiecutter.AWS_region}}  --capabilities CAPABILITY_IAM
git init
git add .
git commit -m "Initial commit"
git remote add origin https://git-codecommit.{{cookiecutter.AWS_region}}.amazonaws.com/v1/repos/{{cookiecutter.project_name_process}}
git push -u origin master

echo "Waiting for {{cookiecutter.project_name_process}}-Staging stack to be created..."
aws cloudformation wait stack-exists --stack-name {{cookiecutter.project_name_process}}-Staging --region {{cookiecutter.AWS_region}} 
aws cloudformation wait stack-exists --stack-name {{cookiecutter.project_name_process}}-Staging --region {{cookiecutter.AWS_region}} 
aws cloudformation wait stack-exists --stack-name {{cookiecutter.project_name_process}}-Staging --region {{cookiecutter.AWS_region}} 
aws cloudformation wait stack-create-complete --stack-name {{cookiecutter.project_name_process}}-Staging --region {{cookiecutter.AWS_region}} 
aws cloudformation wait stack-create-complete --stack-name {{cookiecutter.project_name_process}}-Staging --region {{cookiecutter.AWS_region}} 
echo "Continuing..."

