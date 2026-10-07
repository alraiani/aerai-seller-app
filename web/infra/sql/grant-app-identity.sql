/*
  grant-app-identity.sql
  Purpose : One-time grant letting the web app's managed identity use the AERaiSeller database.
  Run as  : A member of the Entra ID SQL admin group, connected to the AERaiSeller database
            (e.g. Azure Data Studio / sqlcmd with --authentication-method ActiveDirectoryDefault).
  Inputs  : Replace app-aerai-seller-prod with the webAppName deployment output.
  Notes   : Least privilege — read/write data and execute the promotion procedure. Schema changes
            are applied by CI (migrations bundle) as the admin group, not by the app.
*/
CREATE USER [app-aerai-seller-prod] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [app-aerai-seller-prod];
ALTER ROLE db_datawriter ADD MEMBER [app-aerai-seller-prod];
GRANT EXECUTE ON SCHEMA::core TO [app-aerai-seller-prod];
GRANT EXECUTE ON SCHEMA::stg TO [app-aerai-seller-prod];
