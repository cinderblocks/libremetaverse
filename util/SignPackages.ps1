<#
 # Copyright (c) 2021, Sjofn LLC. All rights reserved.
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

 param([String]$PfxPasswd)

 if ([string]::IsNullOrEmpty($PfxPasswd)) {
	Write-Output "Certificate Password not supplied. Cannot sign package."
	exit
}
 
 Write-Output "Signing nupkgs..."

Get-ChildItem -Filter "*.nupkg" -Path "C:\Users\appveyor\AppData\Local\Temp\" -recurse | ForEach {
	Write-Output $("Signing " + $_.Name + "...")
	nuget sign $_.FullName -NonInteractive -Verbosity quiet `
						   -CertificateFingerprint 4FC4D098D5CF0C88769B0CE1ED45ABE6B9A8F879 `
						   -CertificateStoreLocation "LocalMachine" `
						   -CertificatePassword $PfxPasswd `
						   -Timestamper http://timestamp.comodoca.com
 }

 Write-Output "Signing complete."
