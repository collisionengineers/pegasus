##### **Intake and automatic matching testing**















###### **Test 3**



**Case ID**:



**Test Goal**: Prove that given for a pre-existing triage case with no images, that images uploaded or received will automatically attach.

**Test Setup Steps:**



1. Triage e-mail sent, no images attached.
2. Images uploaded (manual upload / automation actor route) seperately after Triage case created.



**Expected outcome**: Images automatically attach to the triage case.



**Outcome Breakdown:**



1. Triage case created.
2. Case unable to be resolved/assigned as missing images.
3. After image receipt, images matched to case.
4. No image initiated case created.
5. Triage now able to be resolved on Pegasus



**Actual outcome**:







###### **Test 4**



**Case ID**:



**Test Goal**: Prove that given a pre-existing image initiated case, upon receipt of a triage e-mail with no images, that image initiated case is merged into the Triage case.



**Test Setup Steps:**



1. Images uploaded (manual upload / automation actor route) first.
2. Triage e-mail sent after with matching registration.

&#x20;

**Expected outcome**:   .



1. Image case created.
2. Image case held in "not ready" lacking instructions.
3. Upon triage instruction receipt, images matched to case and image cased merged into triage.
4. Triage able to be resolved.



**Actual outcome**:







###### **Test 5**



**Case ID**:



**Test Goal:** Prove that given a resolved triage case, when instructions are received, that triage case is automatically assosciated to the new instruction case.

**Test Setup Steps**:



1. Triage e-mail with images received.
2. Triage given outcome/resolution on Pegasus
3. Instruction e-mail with matching registration received with all required details but NO images



Instructions received with resolved triage on system matching instructions.



Triage with Images e-mail sent.



Triage resolved on Pegasus with outcome.



Instruction e-mail received afterwards with all required details.



**Expected outcome**:



1. Triage case created and resolvable.
2. After instruction e-mail receipt, instruction case created.
3. Triage case assosciated with instruction case and showing in its history.
4. Triage images included in new intruction cases evidence.
5. Assuming all other details succesfully extracted, instruction case lands in "Review" Queue



**Actual outcome**:







###### **Test 6**



**Case ID**:



**Test Goal**: Prove that given an unresolved triage, when instructions are received, this triage is NOT merged into the instructions case, but does appear as a suggested link.



**Test Setup and Requirements:**



1. Instructions received with unresolved triage on system matching instructions.
2. Triage with Image e-mail sent.
3. Triage NOT resolved or recorded with outcome.
4. Instruction e-mail received afterwards.



**Expected outcome**:



Triage case merged into instruction case. Assuming all other details, instruction case lands in "Review" Queue.



1. Triage case created.
2. Instruction case created.
3. Triage not merged into instruction case.
4. Appears as "suggested link with option to resolve triage and link to instruction.



**Actual outcome**:





###### **Test 7**



**Case ID**:



**Test Details**: Test 6 follow-on. Prove that given an unresolved triage, when instructions are received, this triage is NOT merged into the instructions case, but does appear as a suggested link on both the triage and the instruction case. Triage then resolved on the system. This should offer confirmation to link the two.



1. Instructions received with unresolved triage on system matching instructions.
2. Triage with Image e-mail sent.
3. Triage NOT resolved or recorded with outcome.
4. Instruction e-mail received afterwards.
5. Triage then resolved on system with outcome



**Expected outcome**:



Triage case merged into instruction case. Assuming all other details, instruction case lands in "Review" Queue.



1. Triage case created.
2. Instruction case created.
3. Triage not merged into instruction case.
4. Appears as "suggested link" with option to resolve triage and link to instruction.
5. When triage is resolved, offer confirmation to link this to existing instructions case.



**Actual outcome**:









###### **Test 8**



**Case ID**:



**Test Goal**: Prove image case merge to triage, then triage to instruction in one continuous case.



**Test Setup and Requirements:**



1. Images uploaded first (manual upload / automation actor route).
2. Image initiated case created
3. Triage e-mail received with matching registration to image upload
4. Triage case created.
5. Triage resolved or recorded with outcome.
6. Instruction e-mail received afterwards.



**Expected outcome**:  Image case merged into Triage Case. Triage case merged into instruction case. Assuming all other details, instruction case lands in "Review" Queue.



**Outcome Breakdown:**



1. Image initiated case created
2. Upon triage e-mail receipt, triage case created.
3. Images automatically matched to Triage
4. Triage not merged into instruction case.
5. Appears as "suggested link with option to resolve triage and link to instruction.



**Actual outcome**:

