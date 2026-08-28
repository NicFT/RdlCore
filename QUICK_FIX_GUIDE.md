# ⚡ Guia Rápido: Como Resolver o Erro "gdiplus.dll" 

## Tldr; (Versão Ultra-Rápida)

```bash
# 1. Limpe tudo
dotnet clean RdlCore.sln
rm -rf bin obj

# 2. Recompile
dotnet build RdlCore.sln -c Release --no-incremental

# 3. Pronto! Agora testa no Linux
dotnet publish -r linux-x64 -c Release
```

---

## O Que Aconteceu

✅ **Código foi corrigido** para não tentar carregar `gdiplus.dll` no Linux
❌ **Você precisa recompilar** a DLL com o código corrigido

## Por Que Está Falhando Ainda

Você está usando uma **DLL compilada com código antigo** que ainda chama `System.Drawing.Image.FromStream()`.

A correção foi implementada **no código-fonte**, mas a DLL que você está usando foi compilada **antes da correção**.

## Solução (3 Comandos)

### Windows (PowerShell):
```powershell
# 1. Limpe
dotnet clean RdlCore.sln; Remove-Item -Recurse bin,obj -ErrorAction SilentlyContinue

# 2. Recompile
dotnet restore RdlCore.sln
dotnet build RdlCore.sln -c Release --no-incremental

# 3. Publique para Linux
dotnet publish RdlCore.sln -r linux-x64 -c Release

# A DLL NOVA agora está em:
# RdlCore/Microsoft.ReportViewer.Common/bin/Release/net10.0/linux-x64/publish/
```

### Linux/Mac (Bash):
```bash
# 1. Limpe
dotnet clean RdlCore.sln
rm -rf bin obj

# 2. Recompile
dotnet restore RdlCore.sln
dotnet build RdlCore.sln -c Release --no-incremental

# 3. Publique
dotnet publish RdlCore.sln -r linux-x64 -c Release

# A DLL NOVA agora está em:
# RdlCore/Microsoft.ReportViewer.Common/bin/Release/net10.0/linux-x64/publish/
```

## Teste (Para Confirmar Que Funcionou)

Após compilar, teste isto no seu app:

```csharp
var report = new LocalReport();
report.ReportPath = "relatorio_com_imagem.rdlc";

try 
{
	// Isto vai funcionar agora! (mesmo no Linux)
	byte[] docxBytes = report.Render(
		"WORDOPENXML", 
		deviceInfo: null, 
		mimeType: out _, 
		encoding: out _, 
		fileNameExtension: out _, 
		streams: out _, 
		warnings: out _);

	Console.WriteLine("✅ FUNCIONOU! Problema resolvido!");
}
catch (DllNotFoundException ex) when (ex.Message.Contains("gdiplus"))
{
	Console.WriteLine("❌ Ainda não funcionou. Verifique se recompilou.");
}
```

---

## ⚠️ Pontos Críticos

### ❗ OBRIGATÓRIO:
- [ ] Rodar `dotnet clean` ANTES de compilar
- [ ] Compilar em modo Release (`-c Release`)
- [ ] Usar o arquivo DLL NOVO (não a velha)

### ✓ OPCIONAL:
- Depois de publicar, copiar apenas os arquivos para Linux
- Não precisa instalar nada (self-contained ou aponta para runtime remoto)

---

## Estrutura de Pastas Após Compilação

```
RdlCore/
├── Microsoft.ReportViewer.Common/
│   └── bin/
│       └── Release/
│           └── net10.0/
│               ├── linux-x64/
│               │   └── publish/
│               │       ├── Microsoft.ReportingServices.*.dll  ← NOVOS
│               │       └── ...
│               └── win-x64/
│                   └── publish/
│                       └── ... (Windows)
└── ...
```

**Use a DLL de `linux-x64/publish/` para Linux!**

---

## ❓ E Se Ainda Não Funcionar?

### 1️⃣ Verifique se deletou cache:
```bash
# Windows
Remove-Item -Recurse $env:USERPROFILE\.nuget\packages\microsoft.reporting* -Force

# Linux
rm -rf ~/.nuget/packages/microsoft.reporting*
```

### 2️⃣ Verifique a versão do .NET:
```bash
dotnet --version
# Deve ser 10.x.x
```

### 3️⃣ Procure por "System.Drawing" manualmente:
```bash
# Se encontrar ainda há problema
grep -r "System\.Drawing\.Image\.FromStream" src/
```

### 4️⃣ Última opção - Force clean total:
```bash
# Apague tudo e comece do zero
rm -rf bin obj .vs
dotnet nuget locals all --clear
dotnet restore
dotnet build -c Release --no-incremental
```

---

## 📊 Como Saber Que Funcionou

| Indicador | Antes ❌ | Depois ✅ |
|-----------|---------|----------|
| Erro ao gerar Word | `DllNotFoundException: gdiplus.dll` | Sem erro! |
| Stack trace mostra | `System.Drawing.Image.FromStream` | `ImageProviderFactory` |
| Relatório é gerado | Não | Sim! |

---

## 🎯 Seu Checklist

- [ ] Rodei `dotnet clean RdlCore.sln`
- [ ] Rodei `dotnet build RdlCore.sln -c Release --no-incremental`
- [ ] Testei localmente e funcionou
- [ ] Publiquei para `linux-x64`
- [ ] Copiei a DLL nova para o Linux
- [ ] Testei no Linux e funcionou! ✅

---

## 📞 Precisa de Ajuda?

Se não conseguir fazer funcionar:

1. **Compartilhe o output de**:
   ```bash
   dotnet build RdlCore.sln --verbose 2>&1 | tail -100
   ```

2. **E também**:
   ```bash
   dotnet --version
   uname -a  # Linux
   # ou
   [System.Environment]::OSVersion  # Windows
   ```

3. **E o stack trace completo** do erro

---

## 🚀 Resumo Ultra-Rápido

```
Problema: DLL antiga
Solução: `dotnet clean` + `dotnet build`
Resultado: Sem erro de gdiplus no Linux! ✅
Tempo: ~5 minutos
```

**Agora faça isso!** 💪
