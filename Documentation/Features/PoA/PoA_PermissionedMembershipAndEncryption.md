### Permission membership and protocol encryption

This feature enables encryption of all protocol messages and prevents unauthorized actors from accessing the network.

Enabling this feature requires first setting up a certificate authority server (`Stratis.DLT.CA` project). Information on how to do it can be found in CA's documentation folder. 

This document will focus on how to enable this feature on node's side. 



#### Configuration steps

##### Code changes

In network's `PoAConsensusOptions` set `EnablePermissionedMembership` to `true`.

Copy root certificate provided by CA to daemon's project, name it `AuthorityCertificate.crt` and enable copy on build. 



##### Node's configuration

Create certificate request file and provide it to network's admin who will use CA to generate a certificate which should be then given to you. Combine `crt` certificate with private key into `ClientCertificate.pfx` file using openssl. More info on how to do it using openssl can be found in CA's documentation folder. 



Run the node with following parameters:  

```
-certificatepassword=QWEQWE -caurl=https://localhost:5001
```

replace values with your certificate's password and CA's url. 