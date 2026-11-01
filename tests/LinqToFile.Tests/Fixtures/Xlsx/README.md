# Independent XLSX fixtures

These unmodified fixtures come from the .NET Foundation Open XML SDK test corpus.
Their application metadata identifies Microsoft Excel.
They provide independent package-layout evidence; this project did not regenerate them.

Upstream repository: https://github.com/dotnet/Open-XML-SDK

Pinned commit: `431ab05cf160248cc3885a4a766026d4f8243792`

Upstream directory: `test/DocumentFormat.OpenXml.Tests.Assets/assets/TestDataStorage/v2FxTestFiles/spreadsheet/`

| File | Coverage |
|---|---|
| SST.xlsx | Shared-string header, physical row 4, header-only sheet |
| CalcChain.xlsx | Cached formula header, physical row 5, header-only sheet |
| CF.xlsx | Native numeric header/data and ignored conditional formatting |

These files are covered by the upstream MIT license, copied as LICENSE.MIT.
The project runtime remains Apache-2.0 and does not depend on the Open XML SDK.
The regression tests run without network access.

SHA-256 checksums:

```text
c64e90aa1acb81a3b110a0a0dd794a5faf4a234b63c300755479ca54a0e623ef  SST.xlsx
cda1567a139ab14bdf39d0e3f16089b9d557fadd138ce67b982e43e2a93b4cc6  CalcChain.xlsx
178903f3d1c053fc7e062a34e441c1192dbcad1173c6aa63db555fabea430bc0  CF.xlsx
```
