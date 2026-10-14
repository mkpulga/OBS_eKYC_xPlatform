# Hardware and reporting findings

These findings are based on inspected source and vendor/library documentation. No biometric device, camera quality engine, signature pad, report exporter, or generated export file was tested.

| Integration | Evidence | Blocker |
|---|---|---|
| Futronic FS88H | Source uses FutronicHandler/ftrScanAPI and image capture/segmentation; vendor SDK documents capture, template extraction, and matching | Exact SDK build/driver/redistribution, FS88H device access, backend template/image encoding, finger positions/counts/quality, and cross-device duplicate matching need confirmation |
| Topaz | Original project references SigPlusNET; Signature.cs captures `GetSigImage()` with image sizing | Exact pad model/connection, compatible .NET 9 adapter strategy, license terms, and backend signature acceptance need verification |
| Face quality/ICAO | IcaoInformation.cs references `obsDLL_v3.FaceRecognition.IcaoFace`; source loads `onejkit.lic` | Engine ownership/license/redistribution, native dependencies, actual ICAO version/tests/thresholds, and Android compatibility are not established |
| Mouse/touch signature | Required by revised scope | Drawing UI, nonblank/image validation, backend image encoding, permission/persistence implementation, and platform tests remain unfinished |

A blocked capture adapter throws NotSupportedException and never returns simulated biometric bytes. Windows fingerprint, Android camera/touch signatures, and Windows camera/pad/mouse integrations still need actual platform adapters. Existing Windows DLLs aren't evidence of Android support. Raw fingerprint images aren't displayed or included in report exports by this foundation.

The [Futronic SDK page](https://www.futronic-tech.com/pro-detail.php?pro_id=1555) describes extraction/matching as well as image capture; this doesn't establish compatibility with the existing backend. [SigPlusNET](https://topazsystems.com/sdks/sigplusnet.html) is a vendor SDK, and its [guide](https://www.topazsystems.com/software/download/sigplusnet.pdf) identifies the Windows Forms control dependency. Topaz distribution must follow the [vendor software agreement](https://topazsystems.com/software-agreements.html); availability without a purchase price isn't unrestricted open-source licensing. Vendor software is not bundled here.

Reporting remains **unimplemented and unproven**. Ten RDLC templates have been inventoried as field/layout references, including collection and individual cardholder/residence/ID reports. See `evidence/report-inventory.json`. Their original rendering engine is not being assumed compatible with MAUI or a browser.

Candidates for a feasibility spike, **not committed dependencies**:

| Format | Candidate | License evidence | Required proof |
|---|---|---|---|
| PDF/layout preview | PDFsharp/MigraDoc Core | [MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE) | Fonts, images, layout, pagination, and browser WASM/Android execution |
| Word DOCX | Open XML SDK | [MIT](https://github.com/dotnet/Open-XML-SDK/blob/main/LICENSE) | PAGE/NUMPAGES fields, image/layout mapping, browser execution, viewer field refresh |
| Excel XLSX | Open XML SDK | Same MIT project | Print areas, page setup, repeated headers, `&P`/`&N` print footer, browser execution |

PDFsharp and Open XML licensing permits distribution subject to retaining notices; exact transitive dependency/font licenses must be reviewed for any chosen versions. No paid reporting library or restricted free tier has been added. No exporter package version is claimed built or tested.

The representative spike must generate one list and one individual report across Windows, Android, and a **browser-side** exporter from authorized existing API data. Prove preview and all PDF/Word/Excel formats before selecting packages. Interactive Server authentication doesn't authorize moving reporting onto the host. A browser-only .NET WASM reporting component may be required; its data-transfer/authorization boundary and package compatibility remain to be implemented. No new reporting endpoints are planned.

`ReportContract` defines the mandatory footer and explicit selected-record/current-page/all-filtered scope. It rejects an all-filtered export unless the dataset is known complete. The timestamp is Asia/Manila's fixed UTC+08:00 offset with seconds. PDF exporters must use real page counts; Word must emit PAGE/NUMPAGES fields; Excel must use actual print page fields. Excel footer acceptance concerns print preview/printed pages. The contract alone is not a working export and does not prove pagination. Word/Excel remain editable. There is no footer-disable setting.

The .NET 9 retarget resolved the earlier web framework assets and native compilation blockers: Windows, Android, and web now build. The representative report proof is still blocked by unimplemented exporters, unverified browser package compatibility, limited external restore/backend access, and no real platform/viewer/device tests. No required format has been dropped and no reporting server has been introduced. If the browser spike cannot generate a required format, record that exact format/platform failure and request a focused scope decision before changing this design.
