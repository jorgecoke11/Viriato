import { File, FileArchive, FileImage, FileSpreadsheet, FileText, FileVideo, type LucideIcon } from 'lucide-react'

/** The icon that says what kind of file it is, from its name and content type. The same everywhere a file is listed. */
function iconoDeArchivo(nombre: string, contentType?: string | null): LucideIcon {
  const tipo = (contentType ?? '').toLowerCase()
  const extension = nombre.includes('.') ? nombre.split('.').pop()!.toLowerCase() : ''
  if (tipo.startsWith('image/') || ['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg'].includes(extension)) return FileImage
  if (tipo.startsWith('video/') || ['mp4', 'webm', 'mov', 'avi'].includes(extension)) return FileVideo
  if (['csv', 'xls', 'xlsx', 'ods'].includes(extension) || tipo.includes('spreadsheet') || tipo.includes('excel') || tipo === 'text/csv') return FileSpreadsheet
  if (['zip', 'rar', '7z', 'gz', 'tar'].includes(extension) || tipo.includes('zip')) return FileArchive
  if (['pdf', 'doc', 'docx', 'txt', 'md', 'json', 'xml', 'odt'].includes(extension) || tipo.startsWith('text/') || tipo === 'application/pdf') return FileText
  return File
}

/** A file's icon in a soft square tile, to lead a row or a card. */
export function FileTypeIcon({ nombre, contentType, className = '' }: { nombre: string; contentType?: string | null; className?: string }) {
  const Icono = iconoDeArchivo(nombre, contentType)
  return (
    <span aria-hidden="true" className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-indigo-100 text-indigo-600 ${className}`}>
      <Icono size={20} />
    </span>
  )
}
