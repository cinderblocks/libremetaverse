<#
 # Copyright (c) 2020, Sjofn LLC. All rights reserved.
 #
 # Permission to use, copy, modify, and/or distribute this script for any
 # purpose without fee is hereby granted.
 # 
 # THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES 
 # WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 # MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR 
 # ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES 
 # WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN 
 # ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF 
 # OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE. 
 #>

param([String]$PfxDownloadUrl, [String]$PfxDownloadUser, [String]$PfxDownloadPasswd, [String]$PfxPasswd)
Write-Output "Downloading signing certificate..."

$secure_pwd = ConvertTo-SecureString -String $PfxDownloadPasswd -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential ($PfxDownloadUser, $secure_pwd)

Invoke-WebRequest -Uri $PfxDownloadUrl -Credential $credential -OutFile "tmp.pfx"

Write-Output "Importing signing certificate..."
$secure_pwd = ConvertTo-SecureString -String $PfxPasswd -AsPlainText -Force
Import-PfxCertificate -FilePath "tmp.pfx" -CertStoreLocation cert://LocalMachine/My -Password $secure_pwd

Remove-Item "tmp.pfx"
