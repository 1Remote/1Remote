/**
 * 运行器领域纯逻辑（自 RunnerGroup.vue 拆出，供 RunnerGroup 与 RunnerCard 共用）：
 *
 * - 字段存在性探针：runners 是 PascalCase 直通域（GET 原样往返），按"属性存在性"
 *   渲染配置位，不硬编码 $type 名单——兼容 PuttyRunner / KittyRunner[Obsolete] /
 *   InternalDefaultRunner / ExternalRunner(ForSSH) 的字段差异。
 * - autoArguments：ExePath 变化时的预设参数自动填充，WPF
 *   ExternalRunnerSettingsViewModel.AutoArguments 的前端移植（WPF 里属 VM 层逻辑而非
 *   后端常量，故前端移植而非经 meta 下发）。触发条件与 WPF 一致：仅当 Arguments 为空
 *   才填（不覆盖用户已填参数）；文件名（完整路径取最后一段）忽略大小写子串命中——WPF 用
 *   FileInfo(path).Name.IndexOf(...)，此处 split 等价；命中分支按 WPF 顺序互斥（WPF 靠
 *   "Arguments 已非空"短路后续分支，此处 return）。ArgumentsForPrivateKey 仅 SSH 族
 *   runner 有该属性（hasArgsPrivateKey 探针）。
 */

// 外部运行器判定：$type 含 ExternalRunner（ExternalRunner / ExternalRunnerForSSH）
export const isExternal = (r) => !!r && String(r.$type || '').includes('ExternalRunner')

export const hasExePath = (r) => r.ExePath !== undefined
export const hasTheme = (r) => r.PuttyThemeName !== undefined
export const hasFont = (r) => r.PuttyFont !== undefined
export const hasFontSize = (r) => r.PuttyFontSize !== undefined
export const hasCharset = (r) => r.LineCodePage !== undefined
// 内置运行器是否有可配置位（无则退回只读说明行）
export const hasInternalConfig = (r) =>
  !isExternal(r) && (hasExePath(r) || hasTheme(r) || hasFont(r) || hasFontSize(r) || hasCharset(r))
// SSH 族外部运行器才有 ArgumentsForPrivateKey（ExternalRunnerForSSH 属性存在性判断）
export const hasArgsPrivateKey = (r) => isExternal(r) && r.ArgumentsForPrivateKey !== undefined

// 预设自动填充：r.OwnerProtocolName 缺失时用 fallbackProto（调用方的当前页签协议键）
export function autoArguments(r, fallbackProto) {
  if (String(r.Arguments || '') !== '') return
  const p = r.OwnerProtocolName || fallbackProto
  const name =
    String(r.ExePath || '')
      .split(/[\\/]/)
      .pop() || ''
  const lower = name.toLowerCase()
  const setPk = (v) => {
    if (hasArgsPrivateKey(r)) r.ArgumentsForPrivateKey = v
  }
  if (lower.includes('winscp')) {
    if (p === 'FTP') r.Arguments = 'ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%'
    else if (p === 'SFTP') {
      r.Arguments = 'sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%'
      setPk('sftp://%1RM_USERNAME%@%1RM_HOSTNAME%:%1RM_PORT% /privatekey=%1RM_PRIVATE_KEY_PATH%')
    }
    r.RunWithHosting = true
    return
  }
  if (lower.includes('filezilla')) {
    if (p === 'FTP') r.Arguments = 'ftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%'
    else if (p === 'SFTP') r.Arguments = 'sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%'
    r.RunWithHosting = false
    return
  }
  if (p === 'SSH' && lower.includes('kitty')) {
    r.Arguments =
      '-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION% -cmd "%STARTUP_AUTO_COMMAND%"'
    setPk('') // WPF：kitty 私钥参数 NOT SUPPORTED
    r.RunWithHosting = true
    return
  }
  if (p === 'SSH' && lower.includes('putty')) {
    r.Arguments = '-ssh %1RM_HOSTNAME% -P %1RM_PORT% -l %1RM_USERNAME% -pw %1RM_PASSWORD% -%SSH_VERSION%'
    setPk('') // WPF：putty 私钥参数 NOT SUPPORTED
    r.RunWithHosting = true
    return
  }
  if (lower === 'wt.exe' || lower === 'wt') {
    if (p === 'SSH') {
      r.Arguments =
        '-w 1 new-tab --title "%1RM_HOSTNAME%" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -pw %1RM_PASSWORD%'
      if (String(r.ArgumentsForPrivateKey || '') === '')
        setPk(
          '-w 1 new-tab --title "%1RM_HOSTNAME%" --suppressApplicationTitle plink -ssh %1RM_HOSTNAME% -P %1RM_PORT% -%SSH_VERSION% -C -X -no-antispoof -l %1RM_USERNAME% -i %1RM_PRIVATE_KEY_PATH%'
        )
    }
    r.RunWithHosting = false
    return
  }
  if (p === 'VNC' && lower.includes('vpxclient')) {
    r.Arguments = '-s %1RM_HOSTNAME% -u %1RM_USERNAME% -p %1RM_PASSWORD%'
    r.RunWithHosting = true
    return
  }
  if (p === 'VNC' && lower.includes('tvnviewer')) {
    r.Arguments = '%1RM_HOSTNAME%::%1RM_PORT% -password=%1RM_PASSWORD% -scale=auto'
    r.RunWithHosting = true
    return
  }
  if (p === 'VNC' && (lower.includes('vncviewer') || lower.includes('uvnc'))) {
    r.Arguments = '%1RM_HOSTNAME%:%1RM_PORT% -password=%1RM_PASSWORD%'
    r.RunWithHosting = false
  }
}
