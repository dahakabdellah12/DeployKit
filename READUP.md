# DeployKit — دليل التحديث التلقائي

اضبط مشروعك عشان يستقبل التحديثات من **DeployKit Cloud** بثلاثة أسطر فقط.

---

## 1. إضافة المرجع للمشروع

**في ملف `.csproj`** لمشروعك:

```xml
<ItemGroup>
  <!-- إذا DeployKit جنب مشروعك -->
  <ProjectReference Include="..\DeployKit\DeployKit.Integration\DeployKit.Integration.csproj" />

  <!-- أو بعد ما تنزله من NuGet -->
  <!-- <PackageReference Include="DeployKit.Integration" Version="1.0.0" /> -->
</ItemGroup>
```

> المتطلب: المشروع لازم يكون `net8.0-windows` لأن SDK يستخدم WPF لنافذة التحديث.

---

## 2. تفعيل SDK (سطر واحد)

**في `App.xaml.cs`** (أو نقطة بداية تطبيقك):

```csharp
using DK = DeployKit.Integration.DeployKit;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ✨ سطر التفعيل — ضع مفتاح API والرابط
        DK.Configure("YOUR_API_KEY", "https://deploykit-api.onrender.com");
    }
}
```

`Configure` تسوي ثلاث أشياء تلقائياً:
1. تقرأ إصدار تطبيقك من الـ Assembly (مثلاً `1.0.0.0`)
2. تفحص السحابة في الخلفية: `GET /v1/check?key=...&v=1.0.0`
3. إذا فيه تحديث ← تظهر **نافذة التحديث** تلقائياً

---

## 3. ماذا يحدث عند وجود تحديث؟

```
تطبيقك شغال
    │
    ├── SDK يفحص السحابة (خلفية)
    │
    ├── وجد تحديث v1.0.1 → يظهر UpdateWindow
    │         ┌─────────────────────┐
    │         │  يوجد تحديث جديد!   │
    │         │                     │
    │         │  v1.0.0 → v1.0.1   │
    │         │                     │
    │         │  [⏬ تحديث الآن]    │
    │         │  [⏰ لاحقاً]        │
    │         └─────────────────────┘
    │
    ├── ضغط "تحديث الآن" →
    │   1. SDK يحمل الحزمة من السحابة
    │   2. يشغل updater.exe
    │   3. ينهي تطبيقك
    │
    └── updater.exe →
        1. ينتظر تطبيقك يقفل نهائياً
        2. يفك الحزمة (يضيف ملفات / يعدل / يحذف)
        3. يعيد تشغيل تطبيقك بالإصدار الجديد
```

---

## 4. فحص يدوي (اختياري)

إذا حاب تتحكم بمتى يظهر التنبيه بدال ما يظهر تلقائياً:

```csharp
// لا تستخدم Configure() — استخدم CheckAsync() يدوياً
DK.Configure("key", "url");  // مرة واحدة عند البداية

// في أي وقت:
var result = await DK.CheckAsync();

if (result.HasUpdate)
{
    MessageBox.Show($"يوجد تحديث v{result.LatestVersion}!");
}
else
{
    MessageBox.Show("لا توجد تحديثات");
}
```

---

## 5. رفع تحديث جديد للسحابة

### عن طريق DeployKit.Gui (مرئي)
1. افتح `DeployKit.Gui.exe`
2. اذهب إلى **Build**
3. اختر المجلد القديم (الإصدار المثبت حالياً)
4. اختر المجلد الجديد (الإصدار المحدث)
5. اضغط **Build** → يبني حزمة الفروقات
6. املأ API Key + Cloud URL
7. اضغط **Upload**

### عن طريق API (للأتمتة)

```powershell
# سجل تطبيقك (مرة واحدة)
$reg = Invoke-RestMethod -Method Post "https://deploykit-api.onrender.com/v1/register?name=MyApp"
$key = $reg.appKey
# ↑ احفظ هذا المفتاح في تطبيقك

# ارفع تحديث جديد
$bytes = [System.IO.File]::ReadAllBytes("C:\path\to\update.zip")
$url = "https://deploykit-api.onrender.com/v1/upload?key=$key&from=1.0.0&to=1.1.0"
Invoke-RestMethod -Uri $url -Method Post -Body $bytes -ContentType "application/octet-stream"
```

```bash
# أو باستخدام curl
curl -X POST "https://deploykit-api.onrender.com/v1/upload?key=YOUR_KEY&from=1.0.0&to=1.1.0" \
  --data-binary "@update.zip" \
  -H "Content-Type: application/octet-stream"
```

---

## 6. API Reference

| المسار | الطريقة | الوصف |
|---|---|---|
| `/v1/register?name=X` | POST | تسجيل تطبيق جديد ← يرجع `appKey` |
| `/v1/upload?key=X&from=X&to=X` | POST | رفع حزمة تحديث (raw body = ZIP) |
| `/v1/check?key=X&v=X.X.X` | GET | فحص وجود تحديث |
| `/v1/dl/{id}` | GET | تحميل حزمة التحديث |

---

## 7. مثال كامل (تطبيق WPF)

```csharp
// App.xaml.cs
using DK = DeployKit.Integration.DeployKit;

namespace MyApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // تفعيل التحديثات التلقائية
        DK.Configure("a1b2c3d4e5f6", "https://deploykit-api.onrender.com");
    }
}
```

```xml
<!-- MyApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DeployKit\DeployKit.Integration\DeployKit.Integration.csproj" />
  </ItemGroup>
</Project>
```

> الإصدار (Version) في `.csproj` هو اللي SDK يقرأه ويرسله للسحابة.

---

## 8. هيكل حزمة التحديث

يعمل على **حذف و إضافة و تعديل** الملفات:

```
update.zip
├── manifest.json          ← يحتوي على قائمة التغييرات
├── file1.dll              ← ملف مضاف أو معدل
├── folder\file2.dll       ← ملف في مجلد فرعي
├── patches\hash.patch     ← تصحيحات ثنائية (للحجم الصغير)
```

- **Deleted**: SDK يحذفهم من مجلد التطبيق
- **Added**: SDK ينشئهم
- **Modified**: SDK يستبدلهم (أو يطبق patch)
- **Unchanged**: SDK يتجاهلهم

---

## 9. حل المشاكل

| المشكلة | الحل |
|---|---|
| SDK لا يجد التحديث | تأكد من `Version` في `.csproj` يطابق الإصدار القديم في السحابة |
| خطأ `404` من السحابة | تأكد من Cloud URL صحيح، ومن أن السحابة مشغلة |
| التطبيق لا يشتغل بعد التحديث | تأكد أن `updater.exe` موجود بجنب التطبيق (SDK ينسخه تلقائياً) |
| ما ظهرت نافذة التحديث | SDK يفحص مرة واحدة عند البداية فقط؛ استخدم `CheckAsync()` يدوياً |

---

> صنع بواسطة [DeployKit](https://github.com/dahakabdellah12/DeployKit)
