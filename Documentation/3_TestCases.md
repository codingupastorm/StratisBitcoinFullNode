## CA test cases

#### #1 - Check documentation and follow setup instructions

Ensure that all steps described in documentation are reproducible. After following it you should have working CA setup with your own root certificate. 



#### #2 - Check that every controller's method works as described in xmldoc. 

Check all API endpoints provided by the following controllers: `AccountsController`, `CertificatesController`, `HelpersController`. Make sure that if xmldoc says that some access level is required user without this access level will always get an execution error while trying to call that API endpoint.



#### #3 - Check that Admin privileges
Check that Admin's account's access level can't be modified.

Check that Admin's account can't be deleted.

Check that Admin's account is created on first launch (first launch is when there are no accounts in db and `settings.CreateAdminAccountOnCleanStart` is set to true) unless creation of admin'a account was disabled in configuration file.



#### #4 - Check ability to configure the application
Check every setting in `Settings` class and make sure you are able to configure any value using configuration file and that changing the value is reflected in application's behavior according to individual setting's description.



#### #5 - Load check

Ensure that `get_certificate_status` can answer at least 50 calls per second.

Ensure that `get_revoked_certificates` can answer at least 5 calls per second with 2000 certificates being revoked.



Lowest setup that should be used for this test is 8gb RAM, SSD hard drive, 4+ core CPU. 





## P2P encryption and permissioned membership test cases

#### #6 - Ensure 2 nodes with valid certificates can connect to each other

#### #7 - Ensure that node with envoked certificate can't connect to anyone

#### #8 - Ensure that node with certificate that is not signed by root certificate can't connect to anyone

#### #9 - Ensure that a test poa network of >= 10 nodes can function normally if every node has a valid certificate 

