import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/theme/brand_colors.dart';
import '../../../core/widgets/app_widgets.dart';
import '../application/auth_controller.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _mostrarClave = false;

  /// Se enciende si el login tarda. La primera conexion del dia despierta al
  /// servidor y puede llevar cerca de un minuto: sin este aviso parece colgado
  /// y la persona cierra la aplicacion justo antes de que responda.
  bool _tardando = false;
  Timer? _avisoLento;

  @override
  void dispose() {
    _avisoLento?.cancel();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (ref.read(authControllerProvider).cargando) return;
    if (!(_formKey.currentState?.validate() ?? false)) return;

    FocusScope.of(context).unfocus();

    _avisoLento?.cancel();
    _avisoLento = Timer(const Duration(seconds: 6), () {
      if (mounted) setState(() => _tardando = true);
    });

    await ref
        .read(authControllerProvider.notifier)
        .login(
          email: _emailController.text,
          password: _passwordController.text,
        );

    _avisoLento?.cancel();
    if (mounted) setState(() => _tardando = false);
    // La navegacion la decide el router al ver el cambio de sesion: aqui no se
    // navega a mano para que no haya dos fuentes de verdad.
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authControllerProvider);
    final altura = MediaQuery.sizeOf(context).height;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: AppTheme.overlayOnBrand,
      child: Scaffold(
        backgroundColor: BrandColors.surface,
        body: SingleChildScrollView(
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: altura),
            child: IntrinsicHeight(
              child: Column(
                children: [
                  _Header(compacto: altura < 640),
                  Transform.translate(
                    offset: const Offset(0, -28),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 20),
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 440),
                        child: _formCard(context, auth),
                      ),
                    ),
                  ),
                  // Empuja el pie hacia abajo sin separar la tarjeta del
                  // encabezado, que se monta sobre el a proposito.
                  const Expanded(child: SizedBox(height: 16)),
                  Padding(
                    padding: const EdgeInsets.only(bottom: 24),
                    child: Text(
                      '© ${DateTime.now().year} ${AppConfig.companyName}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _formCard(BuildContext context, AuthState auth) {
    final theme = Theme.of(context);

    return Container(
      decoration: BoxDecoration(
        color: BrandColors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: BrandColors.wineDeep.withValues(alpha: 0.12),
            blurRadius: 30,
            offset: const Offset(0, 12),
          ),
        ],
      ),
      padding: const EdgeInsets.fromLTRB(22, 26, 22, 22),
      child: Form(
        key: _formKey,
        child: AutofillGroup(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Iniciar sesión', style: theme.textTheme.headlineSmall),
              const SizedBox(height: 6),
              Text(
                'Usá el mismo correo y contraseña del sistema web.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: BrandColors.textMuted,
                ),
              ),
              const SizedBox(height: 24),
              TextFormField(
                controller: _emailController,
                enabled: !auth.cargando,
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.next,
                autocorrect: false,
                autofillHints: const [AutofillHints.email],
                decoration: const InputDecoration(
                  labelText: 'Correo electrónico',
                  prefixIcon: Icon(Icons.mail_outline_rounded),
                ),
                validator: (value) {
                  final texto = value?.trim() ?? '';
                  if (texto.isEmpty) return 'Escribí tu correo.';
                  if (!texto.contains('@')) {
                    return 'Ese correo no parece completo.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _passwordController,
                enabled: !auth.cargando,
                obscureText: !_mostrarClave,
                textInputAction: TextInputAction.done,
                autofillHints: const [AutofillHints.password],
                onFieldSubmitted: (_) => _submit(),
                decoration: InputDecoration(
                  labelText: 'Contraseña',
                  prefixIcon: const Icon(Icons.lock_outline_rounded),
                  suffixIcon: IconButton(
                    icon: Icon(
                      _mostrarClave
                          ? Icons.visibility_off_outlined
                          : Icons.visibility_outlined,
                    ),
                    tooltip: _mostrarClave
                        ? 'Ocultar contraseña'
                        : 'Mostrar contraseña',
                    onPressed: () =>
                        setState(() => _mostrarClave = !_mostrarClave),
                  ),
                ),
                validator: (value) =>
                    (value ?? '').isEmpty ? 'Escribí tu contraseña.' : null,
              ),
              AnimatedSwitcher(
                duration: const Duration(milliseconds: 200),
                child: auth.error == null
                    ? const SizedBox(width: double.infinity)
                    : Padding(
                        key: ValueKey(auth.error),
                        padding: const EdgeInsets.only(top: 16),
                        child: InlineNotice(message: auth.error!),
                      ),
              ),
              const SizedBox(height: 22),
              ElevatedButton(
                onPressed: auth.cargando ? null : _submit,
                style: ElevatedButton.styleFrom(
                  disabledBackgroundColor: BrandColors.redPrimary.withValues(
                    alpha: 0.75,
                  ),
                  disabledForegroundColor: BrandColors.white,
                ),
                child: auth.cargando
                    ? const Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(
                              strokeWidth: 2.4,
                              color: BrandColors.white,
                            ),
                          ),
                          SizedBox(width: 12),
                          Text('Ingresando…'),
                        ],
                      )
                    : const Text('Ingresar'),
              ),
              AnimatedSwitcher(
                duration: const Duration(milliseconds: 250),
                child: auth.cargando && _tardando
                    ? const Padding(
                        padding: EdgeInsets.only(top: 14),
                        child: InlineNotice(
                          color: BrandColors.inRoute,
                          icon: Icons.hourglass_top_rounded,
                          message:
                              'Conectando con el servidor. La primera conexión '
                              'del día puede tardar hasta un minuto.',
                        ),
                      )
                    : const SizedBox(width: double.infinity),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.compacto});

  final bool compacto;

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;

    return Container(
      width: double.infinity,
      decoration: const BoxDecoration(
        gradient: BrandColors.headerGradient,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(32)),
      ),
      padding: EdgeInsets.fromLTRB(24, top + (compacto ? 24 : 44), 24, 60),
      child: Column(
        children: [
          BrandLogo(size: compacto ? 96 : 124),
          const SizedBox(height: 18),
          const Text(
            AppConfig.companyName,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: BrandColors.white,
              fontSize: 28,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.4,
            ),
          ),
          const SizedBox(height: 6),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
            decoration: BoxDecoration(
              color: BrandColors.white.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(999),
            ),
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  Icons.local_shipping_outlined,
                  size: 16,
                  color: BrandColors.goldLight,
                ),
                SizedBox(width: 6),
                Text(
                  AppConfig.appTagline,
                  style: TextStyle(
                    color: BrandColors.goldLight,
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
