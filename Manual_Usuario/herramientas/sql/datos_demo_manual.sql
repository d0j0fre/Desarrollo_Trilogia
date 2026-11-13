SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON;
DECLARE @admin INT=(SELECT UsuarioId FROM Usuarios WHERE Correo='admin@labodega.cr'),
        @ger INT=(SELECT UsuarioId FROM Usuarios WHERE Correo='gerente@labodega.cr'),
        @emp INT=(SELECT UsuarioId FROM Usuarios WHERE Correo='empleado@labodega.cr'),
        @cho INT=(SELECT UsuarioId FROM Usuarios WHERE Correo='chofer@labodega.cr');
EXEC dbo.sp_Rutas_Create @Zona=N'San José Centro', @ChoferUsuarioId=@cho, @VehiculoId=1, @Observaciones=N'Entregar antes de las 11 a. m.',
     @PedidosJson=N'[{"pedidoId":5},{"pedidoId":9}]', @CreadaPorUsuarioId=@ger, @CreadaPorNombre=N'Gerente Demo';
DECLARE @ruta INT=(SELECT MAX(RutaId) FROM Rutas);
EXEC dbo.sp_Rutas_Dispatch @RutaId=@ruta, @UsuarioId=@ger, @UsuarioNombre=N'Gerente Demo';
DECLARE @conv INT;
EXEC dbo.sp_Chat_GetOrCreateConversation @UsuarioActualId=@admin, @OtroUsuarioId=@ger;
SET @conv=(SELECT ConversacionId FROM ChatConversaciones WHERE UsuarioMenorId=IIF(@admin<@ger,@admin,@ger) AND UsuarioMayorId=IIF(@admin<@ger,@ger,@admin));
EXEC dbo.sp_Chat_SendMessage @ConversacionId=@conv, @RemitenteId=@admin, @Contenido=N'Buenos días. Ya quedó cargado el inventario de la semana.';
EXEC dbo.sp_Chat_GetOrCreateConversation @UsuarioActualId=@ger, @OtroUsuarioId=@emp;
SET @conv=(SELECT ConversacionId FROM ChatConversaciones WHERE UsuarioMenorId=IIF(@emp<@ger,@emp,@ger) AND UsuarioMayorId=IIF(@emp<@ger,@ger,@emp));
EXEC dbo.sp_Chat_SendMessage @ConversacionId=@conv, @RemitenteId=@ger, @Contenido=N'Por favor revise los pedidos pendientes de hoy.';
EXEC dbo.sp_Chat_GetOrCreateConversation @UsuarioActualId=@admin, @OtroUsuarioId=@emp;
SET @conv=(SELECT ConversacionId FROM ChatConversaciones WHERE UsuarioMenorId=IIF(@emp<@admin,@emp,@admin) AND UsuarioMayorId=IIF(@emp<@admin,@admin,@emp));
EXEC dbo.sp_Chat_SendMessage @ConversacionId=@conv, @RemitenteId=@admin, @Contenido=N'Recuerde registrar su jornada al terminar el turno.';
