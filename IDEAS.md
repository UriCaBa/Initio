# Initio — Future Ideas & Roadmap

Este documento detalla las ideas y características propuestas para expandir **Initio** y convertirla en la herramienta definitiva para la configuración inicial de un PC. Las ideas están ordenadas por **prioridad** y, dentro de cada nivel, por **esfuerzo** (de menor a mayor).

> ✅ **Catálogo Remoto** ya implementado — El Store carga apps desde un JSON en GitHub (`catalog.json`) con cache local y fallback embebido.

---

## 🔴 Prioridad Alta

### 1. Windows Debloater (Limpieza) 🧹 — Esfuerzo: Medio
*   **Eliminación de Bloatware:** Lista de selección para desinstalar aplicaciones pre-instaladas innecesarias (ej: Candy Crush, Disney+, solitarios, etc.).
*   **Optimización de Telemetría:** Desactivar servicios de recolección de datos de Microsoft para mejorar la privacidad y liberar recursos.
*   **Limpieza de Menú Inicio:** Automatizar la remoción de iconos anclados de "recomendados" o apps promocionales.

### 2. Exportar / Importar Perfil 📤📥 — Esfuerzo: Medio
*   **Exportar** la lista de apps seleccionadas + ajustes a un archivo `.json` o `.initio`.
*   **Importar** el perfil en otro PC y aplicar toda la configuración con un click.
*   **Caso de uso:** "Configuro mi PC gaming, exporto el perfil, y se lo paso a un amigo".

### 3. First Boot Wizard ✔️ — Esfuerzo: Alto
*   Panel estilo **wizard paso a paso** para usuarios no técnicos:
    1.  ¿Qué tipo de usuario eres? (Gamer / Dev / Office / Casual)
    2.  Apps recomendadas según perfil
    3.  Ajustes del sistema sugeridos
    4.  Resumen y ejecución
*   Mejora drásticamente el UX y posiciona a Initio como un producto más completo.

---

## 🟡 Prioridad Media

### 4. Personalización del Sistema 🎨 — Esfuerzo: Bajo
*   **Temas:** Alternar entre Modo Claro/Oscuro del sistema con un solo click.
*   **Configuración del Explorador:**
    *   Mostrar extensiones de archivos.
    *   Mostrar archivos y carpetas ocultos.
    *   Desactivar el historial de archivos recientes.
*   **Barra de Tareas (Win11):** Alinear iconos a la izquierda o al centro automáticamente.

### 5. Activación de Windows Features 🪟 — Esfuerzo: Bajo
*   Checkboxes para activar features que vienen desactivadas por defecto:
    *   **Hyper-V**
    *   **Windows Sandbox**
    *   **.NET Framework 3.5** (necesario para muchos juegos/apps legacy)
    *   **OpenSSH Client/Server**
*   Implementación directa con `Enable-WindowsOptionalFeature`.

### 6. Perfil de Desarrollador Avanzado 💻 — Esfuerzo: Medio
*   **WSL2:** Automatizar `wsl --install` y elección de distro (Ubuntu/Debian).
*   **Git Config:** Formulario para configurar `user.name` y `user.email` globalmente.
*   **Variables de Entorno:** Añadir rutas comunes (Java Home, Python Path) de forma visual.
*   **Terminal:** Personalización del perfil de Windows Terminal.

### 7. Restaurar Configuraciones de Apps ⚙️ — Esfuerzo: Medio
*   Detectar backups de configuración de apps populares:
    *   **VS Code** → restaurar `settings.json` + extensiones (`code --install-extension`).
    *   **Windows Terminal** → restaurar `settings.json`.
    *   **Firefox/Chrome** → recordatorio para activar Sync.
*   Muy potente combinado con el Perfil de Desarrollador.

---

## 🟢 Prioridad Baja

### 8. Configuración de Red Básica 🌐 — Esfuerzo: Bajo
*   Cambiar nombre del PC (`Rename-Computer`).
*   Configurar DNS preferido (Cloudflare `1.1.1.1`, Google `8.8.8.8`, etc.).
*   Activar/desactivar Network Discovery.

### 9. Rendimiento 🚀 — Esfuerzo: Bajo
*   **Planes de Energía:** Configurar el sistema en "Alto Rendimiento" con un click.
*   **Apps de Inicio:** Listar y permitir desactivar aplicaciones que impactan el tiempo de arranque.

### 10. Post-Install Cleanup 🧼 — Esfuerzo: Bajo
*   Limpiar instaladores y cache que `winget` dejó tras la instalación masiva.
*   Borrar carpetas `temp` y cache de Prefetch.
*   Enfocado en limpiar residuos del propio proceso de setup, no mantenimiento general.

### 11. Cuentas y Asociaciones 🔗 — Esfuerzo: Bajo
*   Abrir las páginas de login de servicios clave post-instalación:
    *   OneDrive, Google Drive, Discord, Steam, Spotify, etc.
*   Solo abrir links, sin almacenar credenciales (seguro y simple).

---

## ⚪ Opcional

### 12. Gestión de Drivers (Solo GPU) 🛠️ — Esfuerzo: Bajo
*   Instalar **NVIDIA App** o **AMD Software** vía winget (los únicos drivers fiables en winget).
*   Detectar hardware con `Get-PnpDevice` y mostrar qué drivers faltan (⚠️).
*   Botón de acceso directo a **Windows Update** y **Device Manager**.
*   ⚠️ **Nota:** No automatizar instalación de drivers genéricos — riesgo de inestabilidad.

### 13. Security Checklist 🛡️ — Esfuerzo: Bajo
*   Panel de **solo lectura** que muestre el estado actual de seguridad del sistema:
    *   Firewall: ✅ Activo / ❌ Inactivo
    *   Antivirus: ✅ Activo / ❌ Inactivo
    *   UAC: nivel actual
    *   BitLocker: estado
*   **Informar, no modificar** — evita que usuarios novatos desactiven protecciones sin saberlo.

---

> **Nota:** Las ideas de "Cortafuegos y UAC" (bajar niveles de seguridad) y "Telemetría agresiva" se han descartado o suavizado por motivos de seguridad del usuario final.
