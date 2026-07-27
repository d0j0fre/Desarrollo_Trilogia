/*
  Execute two copies in parallel with SQLCMD variables ComboId, Token and Identificacion.
  LocalDB-only fixture. Do not run in shared environments.
*/
EXEC dbo.sp_Store_CreateOrderWithPromotions
    @UsuarioId = 2,
    @TipoEntrega = N'Retiro en tienda',
    @IdentificacionCliente = N'$(Identificacion)',
    @ItemsJson = N'[{"tipo":"Combo","productoId":null,"comboId":$(ComboId),"cantidad":1}]',
    @TokenOperacion = '$(Token)';
