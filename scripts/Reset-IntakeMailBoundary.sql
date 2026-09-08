-- Executed inside the intake wipe transaction, after the Worker is stopped.
-- Approval, onboarding time, subscription identity and sequences are untouched.
IF @@TRANCOUNT = 0 OR @CutoffUtc IS NULL OR DATEPART(TZOFFSET, @CutoffUtc) <> 0
    THROW 51000, 'A UTC cutoff and an active wipe transaction are required.', 1;

UPDATE dbo.ApprovedInboxPollStates
SET StartBoundaryUtc = CASE WHEN StartBoundaryUtc > @CutoffUtc
        THEN StartBoundaryUtc ELSE @CutoffUtc END,
    [Cursor] = NULL,
    DueAtUtc = @CutoffUtc,
    LeaseToken = NULL,
    LeaseExpiresAtUtc = NULL,
    LastCompletedAtUtc = NULL,
    LastFailureCode = NULL;

-- A mailbox may have been approved without ever being polled. Leave its scope
-- unbound: the normal claim path binds it without lowering this cutoff.
INSERT dbo.ApprovedInboxPollStates
    (ApprovedMailboxId, MailboxAddress, ScopeFingerprint, Generation,
     ActivatedAtUtc, StartBoundaryUtc, DueAtUtc)
SELECT mailbox.Id, mailbox.Address, '', mailbox.MailboxGeneration,
    mailbox.ActivatedAtUtc,
    CASE WHEN mailbox.ActivatedAtUtc > @CutoffUtc
        THEN mailbox.ActivatedAtUtc ELSE @CutoffUtc END,
    @CutoffUtc
FROM dbo.ApprovedMailboxes AS mailbox
WHERE mailbox.ActivatedAtUtc IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM dbo.ApprovedInboxPollStates AS state WITH (UPDLOCK, HOLDLOCK)
        WHERE state.ApprovedMailboxId = mailbox.Id);
