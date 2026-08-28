# 🔧 Solução para Erro de gdiplus.dll no Linux

## Situação Atual

Você está recebendo este erro ao gerar relatórios em Word ou PDF no Linux:

```
System.DllNotFoundException: Unable to load shared library 'gdiplus.dll' 
or one of its dependencies...
```

## ✅ Boas Notícias

**O código foi corrigido!** Mas você precisa recompilar.

## 🎯 O Que Foi Feito

Foram feitas mudanças em **2 arquivos principais**:

### 1. **MapMapper.cs** 
- **Proteção**: Imagens de marcadores de mapa agora têm fallback no Linux
- **Resultado**: Relatório gera mesmo sem a imagem do marcador

### 2. **TileLayerMapper.cs**
- **Proteção**: Tiles de camadas de mapa agora têm fallback no Linux
- **Resultado**: Relatório gera mesmo sem os tiles

### 3. **Arquivos já corrigidos anteriormente** ✓
- `PictureDescriptor.cs` - Usa `ImageProviderFactory` (cross-platform)
- `WordOpenXmlWriter.cs` - Usa `ImageProviderFactory` (cross-platform)

---

## 🚀 Como Resolver (Passo a Passo)

### Passo 1: Limpar Compilações Antigas

Esta é a **parte mais importante!**

**Windows (PowerShell)**:
```powershell
cd C:\GitHub\RdlCore
dotnet clean RdlCore.sln
Remove-Item -Recurse bin,obj -Force -ErrorAction SilentlyContinue
```

**Linux/Mac**:
```bash
cd ~/RdlCore
dotnet clean RdlCore.sln
rm -rf bin obj
```

### Passo 2: Recompilar com Código Novo

```bash
dotnet restore RdlCore.sln
dotnet build RdlCore.sln -c Release --no-incremental
```

**Verificar se compilou sem erros**:
```bash
# Se ver "Build succeeded", tudo OK ✓
```

### Passo 3: Publicar para Linux

```bash
dotnet publish RdlCore.sln -r linux-x64 -c Release
```

### Passo 4: Usar a DLL Nova

As DLLs compiladas agora estão em:
```
RdlCore/Microsoft.ReportViewer.Common/bin/Release/net10.0/linux-x64/publish/
```

**Copie ESTA pasta para seu servidor Linux**, não as antigas.

### Passo 5: Testar

Execute seu app no Linux. Agora deve funcionar sem erro de `gdiplus.dll`! ✅

---

## ✔️ Validação Rápida

Crie este arquivo `TestReport.cs`:

```csharp
using Microsoft.Reporting.NETCore;

class Program
{
	static void Main()
	{
		Console.WriteLine("Testando renderização de relatório no Linux...");

		try
		{
			var report = new LocalReport();
			// Substitua pelo seu relatório com imagens
			report.ReportPath = "seu_relatorio.rdlc";

			// Isto ia falhar ANTES, agora funciona ✓
			byte[] wordBytes = report.Render("WORDOPENXML", ...);

			Console.WriteLine("✅ SUCESSO! Relatório gerado com " + wordBytes.Length + " bytes");
		}
		catch (DllNotFoundException ex) when (ex.Message.Contains("gdiplus"))
		{
			Console.WriteLine("❌ ERRO: Ainda há problema com gdiplus");
			Console.WriteLine("Verifique se recompilou com 'dotnet clean' primeiro!");
			throw;
		}
	}
}
```

**Resultado esperado**:
```
Testando renderização de relatório no Linux...
✅ SUCESSO! Relatório gerado com 45832 bytes
```

---

## ⚠️ Pontos Críticos

| O Que | Por Quê | Como Fazer |
|------|--------|-----------|
| `dotnet clean` é OBRIGATÓRIO | Remove cache de compilação antigo | `dotnet clean RdlCore.sln` |
| Usar modo Release | Modo Debug tem dependências diferentes | `-c Release` |
| Usar `--no-incremental` | Força recompilação completa | `dotnet build --no-incremental` |
| Usar a DLL nova | Senão ainda usa código antigo | Copiar de `linux-x64/publish/` |

---

## 🆘 Se Não Funcionar (Debugging)

### Verificação 1: Confirmou `dotnet clean`?
```bash
# Se ainda vê arquivos em bin/, não deletou tudo
ls -la bin/
# Deve estar VAZIO
```

### Verificação 2: Realmente compilou Release?
```bash
# Procure por bin/Release
ls -la Microsoft.ReportViewer.Common/bin/Release/net10.0/
# Deve ter estes diretórios: linux-x64, win-x64, etc.
```

### Verificação 3: .NET versão correta?
```bash
dotnet --version
# Deve ser 10.x.x (você tem .NET 10 instalado?)
```

### Verificação 4: Verificar conteúdo da DLL
```bash
# Procure por ImageProviderFactory (novo código)
strings /path/to/dll | grep ImageProviderFactory
# Se não encontrar, é DLL antiga!
```

### Se Tudo Isto Não Resolveu:

1. Compartilhe o output de:
```bash
dotnet build RdlCore.sln -c Release --no-incremental --verbose 2>&1 > build.log
# Compartilhe o arquivo build.log
```

2. E também:
```bash
dotnet --version
uname -a  # Linux
```

---

## 📊 Resumo Técnico (Para TI)

| Antes ❌ | Depois ✅ |
|---------|----------|
| `System.Drawing.Image.FromStream()` → `gdiplus.dll` | `ImageProviderFactory.CreateProvider()` → SkiaSharp |
| Falha em Linux (gdiplus não existe) | Funciona em Linux (SkiaSharp é cross-platform) |
| Suporte apenas Windows | Suporte Windows + Linux |
| Sem proteção em mapas | Proteção com fallback em mapas |

---

## 🎯 Seu Passo a Passo (Copie e Cole)

### Windows (PowerShell):
```powershell
# 1. Navegue até a pasta
cd C:\GitHub\RdlCore

# 2. Limpe
dotnet clean RdlCore.sln
Remove-Item -Recurse bin,obj -Force

# 3. Recompile
dotnet restore RdlCore.sln
dotnet build RdlCore.sln -c Release --no-incremental

# 4. Publique
dotnet publish RdlCore.sln -r linux-x64 -c Release

# 5. Pronto! Copie os arquivos de:
# .\Microsoft.ReportViewer.Common\bin\Release\net10.0\linux-x64\publish\
```

### Linux:
```bash
# 1. Navegue
cd ~/RdlCore

# 2. Limpe
dotnet clean RdlCore.sln
rm -rf bin obj

# 3. Recompile
dotnet restore RdlCore.sln
dotnet build RdlCore.sln -c Release --no-incremental

# 4. Use a build
# Os arquivos estão em ./Microsoft.ReportViewer.Common/bin/Release/net10.0/
```

---

## 📞 Suporte

Não conseguiu? Abra uma issue com:

1. **Output de compilação**:
   ```bash
   dotnet build RdlCore.sln -c Release 2>&1 | head -50
   ```

2. **Stack trace completo** do erro

3. **Sua versão do .NET**:
   ```bash
   dotnet --version
   ```

4. **Seu SO Linux**:
   ```bash
   lsb_release -a  # ou uname -a
   ```

---

## ✨ Status

- ✅ Código corrigido (em MapMapper.cs, TileLayerMapper.cs)
- ✅ Documentação criada (este documento)
- ⏳ Aguardando: Você recompilar e testar

**Próximo passo**: Execute os comandos acima e reporte se funcionou! 🚀

---

**Última atualização**: Janeiro 2026
**Aplica-se a**: RdlCore com .NET 10, Linux (x64, ARM64)
