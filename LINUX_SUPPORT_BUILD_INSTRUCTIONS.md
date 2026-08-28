# Instruções para Compilar o RdlCore para Suporte Linux (Sem System.Drawing.gdiplus)

## Problema Identificado

O projeto RdlCore foi refatorado para suportar renderização cross-platform no Linux, removendo dependências de `System.Drawing.Image.FromStream()` que causavam erro `DllNotFoundException: gdiplus.dll` no Linux.

## Solução Implementada

As seguintes correções foram aplicadas ao código-fonte:

### 1. **MapMapper.cs** (linha 2204)
- Adicionado `try/catch` para capturar `DllNotFoundException` ao chamar `System.Drawing.Image.FromStream()`
- No Linux, se falhar, retorna string vazia permitindo que o relatório continue sem a imagem

### 2. **TileLayerMapper.cs** (linhas 148, 159)
- Adicionado `try/catch` nos métodos `GetSnapshotTile()` e `GetEmbeddedTile()`
- No Linux, se falhar, retorna `null` permitindo que o processamento continue

### 3. **PictureDescriptor.cs** e **WordOpenXmlWriter.cs**
- Já refatorados para usar `ImageProviderFactory.CreateProvider().LoadImage()` em vez de `System.Drawing.Image.FromStream()`
- Utilizam `CrossPlatformImageProvider` no Linux (baseado em SkiaSharp)

## Como Compilar para Linux

### Passo 1: Limpar Compilações Anteriores

```powershell
# Windows (PowerShell)
dotnet clean RdlCore.sln
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue

# Linux (Bash)
dotnet clean RdlCore.sln
rm -rf bin obj
```

### Passo 2: Restaurar Pacotes NuGet

```bash
dotnet restore RdlCore.sln
```

### Passo 3: Compilar em Modo Release

```bash
# Windows (PowerShell)
dotnet build RdlCore.sln -c Release --no-incremental

# Linux (Bash)
dotnet build RdlCore.sln -c Release --no-incremental
```

### Passo 4: Publicar para Linux

```bash
# Para linux-x64
dotnet publish RdlCore.sln -c Release -r linux-x64 --self-contained false

# Para qualquer plataforma (RID genérico)
dotnet publish RdlCore.sln -c Release -r linux-x64
```

### Passo 5: Validar a Compilação

Verifique se não há erros de compilação e se os arquivos DLL foram gerados corretamente.

## Testando no Linux

Após compilar, teste a renderização de relatórios:

```csharp
// Seu código de renderização
try
{
	var report = new LocalReport();
	report.ReportPath = "path/to/report.rdlc";

	// Este formato deve funcionar agora sem erro de gdiplus
	byte[] pdfBytes = report.Render("PDF", deviceInfo: null, ...);

	// E este também
	byte[] wordBytes = report.Render("WORDOPENXML", deviceInfo: null, ...);
}
catch (DllNotFoundException ex) when (ex.Message.Contains("gdiplus"))
{
	Console.WriteLine("Ainda há uma chamada a System.Drawing que não foi protegida!");
	throw;
}
```

## Detalhe Técnico: Por que a Mudança Funciona

- **Windows**: Continua usando `System.Drawing.Image` normalmente
- **Linux**: 
  - Para imagens em mapa/tiles: fallback gracioso (retorna null/vazio)
  - Para imagens em relatórios: `CrossPlatformImageProvider` usa SkiaSharp (sem dependência de gdiplus)

## Possíveis Problemas Remanescentes

Se ainda receber erro de `gdiplus` após recompilação limpa:

1. **Cache de Build**: Limpe completamente o cache dotnet:
   ```bash
   dotnet nuget locals all --clear
   dotnet clean RdlCore.sln -c Release
   ```

2. **Arquivos Antigos**: Verifique se não há DLLs antigas em cache do sistema:
   ```bash
   # Linux
   find ~/.nuget/packages -name "*.dll" -path "*Drawing*" -o -name "*gdiplus*"

   # Windows
   Get-ChildItem $env:USERPROFILE\.nuget\packages -Recurse -Filter "*Drawing*"
   ```

3. **Verificar Versão**: Confirme que está compilando .NET 10:
   ```bash
   dotnet --version
   ```

## Verificação Final

Para garantir que a compilação está correta:

```bash
# Verifique que o projeto foi compilado
ls -la ./Microsoft.ReportViewer.Common/bin/Release/net10.0/

# Procure por erros de compilação
dotnet build RdlCore.sln --no-restore 2>&1 | grep -i "error"
```

## Suporte

Se o problema persistir após seguir estas instruções:
1. Verifique o relatório completo de compilação: `dotnet build --verbose`
2. Procure por qualquer linha contendo `System.Drawing.Image.FromStream`
3. Abra uma issue com o log completo de compilação e o stack trace de execução
