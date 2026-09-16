allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

// Dónde escribe Gradle sus archivos intermedios.
//
// Por defecto, dentro del proyecto. Pero si el repositorio vive en una carpeta
// sincronizada —OneDrive, Drive, Dropbox— el sincronizador convierte esas
// carpetas en marcadores en la nube y Gradle falla con AccessDeniedException a
// mitad de la compilación, casi siempre en mergeReleaseNativeLibs.
//
// La variable de entorno FLUTTER_ANDROID_BUILD_DIR permite sacar la compilación
// de ahí sin cambiar nada del repositorio ni fijar rutas de una máquina:
//
//   Windows:  $env:FLUTTER_ANDROID_BUILD_DIR = "C:\build\labodega"
//   Linux:    export FLUTTER_ANDROID_BUILD_DIR=/tmp/build/labodega
//
// Sin la variable, el comportamiento es exactamente el de siempre.
val externalBuildDir: String? = System.getenv("FLUTTER_ANDROID_BUILD_DIR")

val newBuildDir: Directory = if (externalBuildDir.isNullOrBlank()) {
    rootProject.layout.buildDirectory.dir("../../build").get()
} else {
    rootProject.layout.projectDirectory.dir(externalBuildDir)
}

rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}
subprojects {
    project.evaluationDependsOn(":app")
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
