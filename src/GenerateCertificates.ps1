$rootCert  =  New-SelfSignedCertificate  `
	-Subject "CN=Root CA,OU=Me,O=MyCompany,L=Brussels,ST=Belgium,C=BE"  `
	-CertStoreLocation cert:\CurrentUser\My `
	-Provider "Microsoft Strong Cryptographic Provider"  `
	-DnsName "MyCompany Root CA"  `
	-KeyLength 2048  `
	-KeyAlgorithm "RSA"  `
	-HashAlgorithm "SHA256"  `
	-KeyExportPolicy "Exportable"  `
	-KeyUsage "CertSign",  "CRLSign"

Export-Certificate -Type CERT -Cert $rootCert -FilePath root-ca.cer

$serverCert = New-SelfSignedCertificate  `
	-Subject "CN=server,OU=ESF,O=Ingenico,L=Brussels,ST=Belgium,C=BE"  `
	-DnsName "server" `
	-CertStoreLocation cert:\CurrentUser\My `
	-Provider "Microsoft Strong Cryptographic Provider"  `
	-Signer $rootCert  `
	-KeyLength 2048  `
	-KeyAlgorithm "RSA"  `
	-HashAlgorithm "SHA256"  `
	-KeyExportPolicy "Exportable"

$password = ConvertTo-SecureString -String "password" -AsPlainText -Force

Export-PfxCertificate -Cert $serverCert -Password $password -FilePath server.pfx
