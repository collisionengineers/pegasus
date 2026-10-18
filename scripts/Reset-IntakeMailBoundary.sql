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

-- The Sent cursor survives; only the boundary advances, so a full delta restart
-- after a Graph 410 cannot retain Sent items from before the cutoff.
UPDATE dbo.ApprovedSentPollStates
SET StartBoundaryUtc = CASE WHEN StartBoundaryUtc > @CutoffUtc
        THEN StartBoundaryUtc ELSE @CutoffUtc END;

-- A Sent-capable mailbox may never have been polled. Same predicate as the
-- Sent claim; the normal claim binds the empty scope without lowering the cutoff.
INSERT dbo.ApprovedSentPollStates
    (MailboxId, MailboxAddress, SentFolderIdentity, ScopeFingerprint,
     Generation, StartBoundaryUtc, DueAtUtc)
SELECT mailbox.MailboxIdentity, mailbox.Address, mailbox.SentFolderIdentity, '',
    mailbox.MailboxGeneration,
    CASE WHEN mailbox.ActivatedAtUtc > @CutoffUtc
        THEN mailbox.ActivatedAtUtc ELSE @CutoffUtc END,
    @CutoffUtc
FROM dbo.ApprovedMailboxes AS mailbox
WHERE mailbox.State = 'Approved'
    AND mailbox.AllowOutgoing = 1
    AND mailbox.MailboxIdentity IS NOT NULL
    AND mailbox.SentFolderIdentity IS NOT NULL
    AND mailbox.ActivatedAtUtc IS NOT NULL
    AND mailbox.MailboxGeneration > 0
    AND NOT EXISTS (
        SELECT 1 FROM dbo.ApprovedSentPollStates AS state WITH (UPDLOCK, HOLDLOCK)
        WHERE state.MailboxId = mailbox.MailboxIdentity);
