-- Executed inside the intake wipe transaction after intake/case rows are gone.
-- The retained production administrator and non-QDOS sequences are untouched.
IF @@TRANCOUNT = 0
    THROW 51010, 'An active wipe transaction is required.', 1;

IF (SELECT COUNT(*) FROM dbo.AspNetUsers WHERE NormalizedUserName = N'ALEX') <> 1
    THROW 51011, 'Exactly one alex account is required.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AspNetUsers AS users
    JOIN dbo.AspNetUserRoles AS userRoles ON userRoles.UserId = users.Id
    JOIN dbo.AspNetRoles AS roles ON roles.Id = userRoles.RoleId
    WHERE users.NormalizedUserName = N'ALEX'
        AND roles.NormalizedName = N'ADMINISTRATOR')
    THROW 51012, 'The alex account must be an Administrator.', 1;

IF (SELECT COUNT(*) FROM dbo.Principals WHERE Code = N'QDOS') <> 1
    THROW 51013, 'Exactly one QDOS principal is required.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.Principals
    WHERE Code = N'QDOS'
        AND SequenceLineageId = @QdosSequenceLineageId)
    THROW 51014, 'The QDOS sequence lineage changed after inventory.', 1;

DECLARE @RemovedUsers TABLE (Id uniqueidentifier PRIMARY KEY);

INSERT @RemovedUsers (Id)
SELECT Id
FROM dbo.AspNetUsers
WHERE NormalizedUserName <> N'ALEX';

DELETE tokens
FROM dbo.OpenIddictTokens AS tokens
JOIN @RemovedUsers AS removed
    ON tokens.Subject = CONVERT(nvarchar(36), removed.Id);

DELETE authorizations
FROM dbo.OpenIddictAuthorizations AS authorizations
JOIN @RemovedUsers AS removed
    ON authorizations.Subject = CONVERT(nvarchar(36), removed.Id);

DELETE events
FROM dbo.SecurityEvents AS events
JOIN @RemovedUsers AS removed
    ON events.SubjectId = CONVERT(nvarchar(36), removed.Id)
        OR events.ActorSubjectId = CONVERT(nvarchar(36), removed.Id);

-- ASP.NET Identity's user children cascade. All other user-bound application
-- rows are non-preserved intake/case tables and were deleted earlier in this
-- transaction before their constraints are checked again.
DELETE users
FROM dbo.AspNetUsers AS users
JOIN @RemovedUsers AS removed ON removed.Id = users.Id;

UPDATE dbo.CaseSequences
SET LastAllocatedSequence = 0
WHERE SequenceLineageId = @QdosSequenceLineageId
    AND [Year] = @QdosSequenceYear;
