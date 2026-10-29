using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Controllers;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class DocumentsPublicationRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_pending_SQL_row_with_storage_failure_is_not_compensated_by_deleting_its_file(bool replacement)
    {
        var f=Fixture(replacement);f.Storage.Setup(x=>x.CommitAsync(f.Staged,It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());
        if(replacement)await f.Controller.ReplaceFile(42,f.File,default);
        else await f.Controller.Create(new(){Title="Synthetic",TypeId=1,DoesNotExpire=true,File=f.File},default);
        f.Storage.Verify(x=>x.DeleteStageAsync(It.IsAny<StagedPrivateFile>()),Times.Never);
        f.Storage.Verify(x=>x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(),It.IsAny<string>()),Times.Never);
        f.Db.Verify(x=>x.DeletePendingDocumentAsync(It.IsAny<int>(),It.IsAny<int>()),Times.Never);
        f.Db.Verify(x=>x.DeletePendingVersionAsync(It.IsAny<int>(),It.IsAny<int>(),It.IsAny<int>()),Times.Never);
        f.Storage.Verify(x=>x.PreserveForReconciliationAsync(f.Staged,42,replacement?73:null,999),Times.Once);
    }

    [Fact]
    public async Task Commit_failure_retains_the_actual_allocated_result_and_does_not_retry_commit()
    {
        int commits=0;var cause=new TimeoutException();string key=new string('a',32)+".pdf";
        var error=await Assert.ThrowsAsync<DocumentPendingCommitUnconfirmedException>(()=>OriginalLoginTransaction.CommitResultAsync(42,
            ()=>{commits++;return Task.FromException(cause);},(id,e)=>new DocumentPendingCommitUnconfirmedException(id,null,key,e)));
        Assert.Equal(1,commits);Assert.Equal(42,error.DocumentId);Assert.Null(error.VersionId);Assert.Equal(key,error.StorageKey);Assert.Same(cause,error.InnerException);
    }

    [Fact]
    public async Task Other_transaction_callers_keep_their_original_commit_exception()
    {
        var cause=new TimeoutException();var error=await Assert.ThrowsAsync<TimeoutException>(()=>OriginalLoginTransaction.CommitResultAsync(42,()=>Task.FromException(cause),null));
        Assert.Same(cause,error);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_pending_commit_keeps_allocated_ids_and_does_not_delete_or_repeat_the_upload(bool replacement)
    {
        var f=Fixture(replacement);
        var lost=new DocumentPendingCommitUnconfirmedException(42,replacement?73:null,f.Staged.StorageKey,new TimeoutException());
        if(replacement)f.Db.Setup(x=>x.ReplaceFilePendingAsync(42,f.Staged,999,"Usuario")).ThrowsAsync(lost);
        else f.Db.Setup(x=>x.CreatePendingAsync(It.IsAny<DocumentFormViewModel>(),f.Staged,999,"Usuario")).ThrowsAsync(lost);
        f.Db.Setup(x=>x.MarkDocumentReadyAsync(42,999,"Usuario")).Returns(Task.CompletedTask);
        f.Db.Setup(x=>x.MarkVersionReadyAsync(42,73,999,"Usuario")).Returns(Task.CompletedTask);
        f.Db.Setup(x=>x.GetPublicationReceiptAsync(42,replacement?73:null,999,f.Staged.StorageKey))
            .ReturnsAsync(new PrivateFileMetadata{OwnerId=42,VersionId=73,StorageKey=f.Staged.StorageKey});
        var result=replacement?await f.Controller.ReplaceFile(42,f.File,default):await f.Controller.Create(new(){Title="Synthetic",TypeId=1,DoesNotExpire=true,File=f.File},default);
        Assert.IsType<RedirectToActionResult>(result);
        f.Storage.Verify(x=>x.CommitAsync(f.Staged,It.IsAny<CancellationToken>()),Times.Once);
        f.Storage.Verify(x=>x.DeleteStageAsync(It.IsAny<StagedPrivateFile>()),Times.Never);
        f.Storage.Verify(x=>x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(),It.IsAny<string>()),Times.Never);
        f.Db.Verify(x=>x.DeletePendingDocumentAsync(It.IsAny<int>(),It.IsAny<int>()),Times.Never);
        f.Db.Verify(x=>x.DeletePendingVersionAsync(It.IsAny<int>(),It.IsAny<int>(),It.IsAny<int>()),Times.Never);
        if(replacement)f.Db.Verify(x=>x.ReplaceFilePendingAsync(42,f.Staged,999,"Usuario"),Times.Once);
        else f.Db.Verify(x=>x.CreatePendingAsync(It.IsAny<DocumentFormViewModel>(),f.Staged,999,"Usuario"),Times.Once);
        Assert.NotNull(f.Controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_after_unknown_pending_commit_preserves_stage_without_a_new_write(bool replacement)
    {
        var f=Fixture(replacement);using var cancellation=new CancellationTokenSource();
        var lost=new DocumentPendingCommitUnconfirmedException(42,replacement?73:null,f.Staged.StorageKey,new TimeoutException());
        if(replacement)f.Db.Setup(x=>x.ReplaceFilePendingAsync(42,f.Staged,999,"Usuario")).Callback(()=>cancellation.Cancel()).ThrowsAsync(lost);
        else f.Db.Setup(x=>x.CreatePendingAsync(It.IsAny<DocumentFormViewModel>(),f.Staged,999,"Usuario")).Callback(()=>cancellation.Cancel()).ThrowsAsync(lost);
        if(replacement)await f.Controller.ReplaceFile(42,f.File,cancellation.Token);
        else await f.Controller.Create(new(){Title="Synthetic",TypeId=1,DoesNotExpire=true,File=f.File},cancellation.Token);
        f.Storage.Verify(x=>x.CommitAsync(It.IsAny<StagedPrivateFile>(),It.IsAny<CancellationToken>()),Times.Never);
        f.Storage.Verify(x=>x.DeleteStageAsync(It.IsAny<StagedPrivateFile>()),Times.Never);
        f.Db.Verify(x=>x.GetPublicationReceiptAsync(It.IsAny<int>(),It.IsAny<int?>(),It.IsAny<int>(),It.IsAny<string>()),Times.Never);
        f.Storage.Verify(x=>x.PreserveForReconciliationAsync(f.Staged,42,replacement?73:null,999),Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persistent_SQL_unavailability_after_pending_commit_keeps_file_and_does_not_reinsert(bool replacement)
    {
        var f=Fixture(replacement);var lost=new DocumentPendingCommitUnconfirmedException(42,replacement?73:null,f.Staged.StorageKey,new TimeoutException());
        if(replacement)f.Db.Setup(x=>x.ReplaceFilePendingAsync(42,f.Staged,999,"Usuario")).ThrowsAsync(lost);
        else f.Db.Setup(x=>x.CreatePendingAsync(It.IsAny<DocumentFormViewModel>(),f.Staged,999,"Usuario")).ThrowsAsync(lost);
        f.Db.Setup(x=>x.GetPublicationReceiptAsync(It.IsAny<int>(),It.IsAny<int?>(),It.IsAny<int>(),It.IsAny<string>())).ThrowsAsync(new TimeoutException());
        if(replacement)await f.Controller.ReplaceFile(42,f.File,default);
        else await f.Controller.Create(new(){Title="Synthetic",TypeId=1,DoesNotExpire=true,File=f.File},default);
        f.Storage.Verify(x=>x.DeleteStageAsync(It.IsAny<StagedPrivateFile>()),Times.Never);
        f.Storage.Verify(x=>x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(),It.IsAny<string>()),Times.Never);
        Assert.NotNull(f.Controller.TempData["ErrorMessage"]);Assert.Null(f.Controller.TempData["SuccessMessage"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_success_does_not_start_unknown_result_recovery(bool replacement)
    {
        var fixture = Fixture(replacement);
        fixture.Db.Setup(x => x.MarkDocumentReadyAsync(42, 999, "Usuario")).Returns(Task.CompletedTask);
        fixture.Db.Setup(x => x.MarkVersionReadyAsync(42, 73, 999, "Usuario")).Returns(Task.CompletedTask);
        if (replacement) await fixture.Controller.ReplaceFile(42, fixture.File, default);
        else await fixture.Controller.Create(new DocumentFormViewModel { Title = "Sintético", TypeId = 1, DoesNotExpire = true, File = fixture.File }, default);
        fixture.Db.Verify(x => x.GetPublicationReceiptAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        fixture.Storage.Verify(x => x.CommitAsync(fixture.Staged, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Storage.Verify(x => x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(), It.IsAny<string>()), Times.Never);
        Assert.Null(fixture.Controller.TempData["ErrorMessage"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_ready_acknowledgement_never_deletes_the_published_file(bool replacement)
    {
        var fixture = Fixture(replacement);
        fixture.Db.Setup(x => x.GetPublicationReceiptAsync(42, replacement ? 73 : null, 999, fixture.Staged.StorageKey))
            .ReturnsAsync(new PrivateFileMetadata { OwnerId = 42, VersionId = 73, StorageKey = fixture.Staged.StorageKey });
        var result = replacement
            ? await fixture.Controller.ReplaceFile(42, fixture.File, default)
            : await fixture.Controller.Create(new DocumentFormViewModel { Title = "Sintético", TypeId = 1, DoesNotExpire = true, File = fixture.File }, default);
        Assert.IsType<RedirectToActionResult>(result);
        fixture.Storage.Verify(x => x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(), It.IsAny<string>()), Times.Never);
        fixture.Db.Verify(x => x.DeletePendingDocumentAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        fixture.Db.Verify(x => x.DeletePendingVersionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        fixture.Db.Verify(x => x.GetPublicationReceiptAsync(42, replacement ? 73 : null, 999, fixture.Staged.StorageKey), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_receipt_keeps_bytes_and_metadata_for_reconciliation(bool replacement)
    {
        var fixture = Fixture(replacement);
        fixture.Db.Setup(x => x.GetPublicationReceiptAsync(42, replacement ? 73 : null, 999, fixture.Staged.StorageKey))
            .ThrowsAsync(new TimeoutException());
        var result = replacement
            ? await fixture.Controller.ReplaceFile(42, fixture.File, default)
            : await fixture.Controller.Create(new DocumentFormViewModel { Title = "Sintético", TypeId = 1, DoesNotExpire = true, File = fixture.File }, default);
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("conserv", fixture.Controller.TempData["ErrorMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        fixture.Storage.Verify(x => x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(), It.IsAny<string>()), Times.Never);
        fixture.Db.Verify(x => x.DeletePendingDocumentAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        fixture.Db.Verify(x => x.DeletePendingVersionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_receipt_for_different_storage_never_confirms_this_upload(bool replacement)
    {
        var fixture = Fixture(replacement);
        fixture.Db.Setup(x => x.GetPublicationReceiptAsync(42, replacement ? 73 : null, 999, fixture.Staged.StorageKey))
            .ReturnsAsync(new PrivateFileMetadata { OwnerId = 42, VersionId = 73, StorageKey = new string('d', 32) + ".pdf" });
        if (replacement) await fixture.Controller.ReplaceFile(42, fixture.File, default);
        else await fixture.Controller.Create(new DocumentFormViewModel { Title = "Sintético", TypeId = 1, DoesNotExpire = true, File = fixture.File }, default);
        Assert.NotNull(fixture.Controller.TempData["ErrorMessage"]);
        Assert.Null(fixture.Controller.TempData["SuccessMessage"]);
        fixture.Storage.Verify(x => x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Cancellation_after_publication_attempt_keeps_bytes_without_a_new_receipt_query()
    {
        var fixture = Fixture(true);
        using var cancellation = new CancellationTokenSource();
        fixture.Db.Setup(x => x.MarkVersionReadyAsync(42, 73, 999, "Usuario"))
            .Callback(() => cancellation.Cancel()).ThrowsAsync(new OperationCanceledException());
        Assert.IsType<RedirectToActionResult>(await fixture.Controller.ReplaceFile(42, fixture.File, cancellation.Token));
        fixture.Storage.Verify(x => x.DeleteCommittedAsync(It.IsAny<PrivateStorageArea>(), It.IsAny<string>()), Times.Never);
        fixture.Db.Verify(x => x.GetPublicationReceiptAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    private static (DocumentsController Controller, Mock<IDocumentManagementDbService> Db,
        Mock<IPrivateFileStorageService> Storage, StagedPrivateFile Staged, IFormFile File) Fixture(bool replacement)
    {
        var db = new Mock<IDocumentManagementDbService>();
        var storage = new Mock<IPrivateFileStorageService>();
        var file = new FormFile(new MemoryStream("%PDF-1.4 synthetic"u8.ToArray()), 0, 18, "File", "synthetic.pdf");
        var staged = new StagedPrivateFile(PrivateStorageArea.Documents, new string('b', 32) + ".stage",
            new string('a', 32) + ".pdf", "synthetic.pdf", "application/pdf", ".pdf", 18, new string('c', 64));
        storage.Setup(x => x.StageAsync(file, PrivateStorageArea.Documents, It.IsAny<CancellationToken>())).ReturnsAsync(staged);
        storage.Setup(x => x.CommitAsync(staged, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        db.Setup(x => x.CreatePendingAsync(It.IsAny<DocumentFormViewModel>(), staged, 999, "Usuario")).ReturnsAsync(42);
        db.Setup(x => x.ReplaceFilePendingAsync(42, staged, 999, "Usuario")).ReturnsAsync(73);
        db.Setup(x => x.MarkDocumentReadyAsync(42, 999, "Usuario")).ThrowsAsync(new TimeoutException());
        db.Setup(x => x.MarkVersionReadyAsync(42, 73, 999, "Usuario")).ThrowsAsync(new TimeoutException());
        db.Setup(x => x.GetOptionsAsync()).ReturnsAsync(((IReadOnlyList<SelectOptionViewModel>)Array.Empty<SelectOptionViewModel>(),
            (IReadOnlyList<SelectOptionViewModel>)Array.Empty<SelectOptionViewModel>()));
        var session = new MemorySession();session.SetInt32("UserId", 999);
        var http = new DefaultHttpContext { Session = session };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "not a SQL destination" }).Build();
        var controller = new DocumentsController(db.Object, storage.Object, Mock.Of<IDocumentAlertService>(),
            new AdminDbService(config, Mock.Of<IPasswordHashService>()), NullLogger<DocumentsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = http }, TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>()) };
        return (controller, db, storage, staged, file);
    }
    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;public string Id => "deterministic";public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
