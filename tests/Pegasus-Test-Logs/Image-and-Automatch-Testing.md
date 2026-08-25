##### **Intake and automatic matching testing**









###### **Test 1**



**Case ID**:



**Test Goal**: Prove that uploaded images automatically match to a corresponding pre-existing instruction case, assuming requirements and caveats met.



**Test Setup and Requirements:**



1. Instruction e-mail sent, no images in e-mail.
2. No image initiated case pre-existing on system that matches.
3. Images uploaded (manual upload / automation actor route) with matching registration to the instructions, after instruction case is auto-created.



Two images were used on this test: 1 contained a full view of vehicle registration, the other contained no viewable registration (same vehicle).



**Expected outcome**: Images automatically matched to existing instructions case. No image initiated case created, as the images are automatically matched to instruction, so this is not required.



**Outcome Breakdown:**



1. Instruction case created.
2. Images match to case.
3. No image initiated case created.
4. Instruction case lands in "Not Ready" upon creation.
5. After image receipt and match, case moves to "Review".



**Actual outcome**:



Instruction case created. Used manual upload for images. Image case was not created, and the images were succesfully matched to the case. 

**Issues**: 



1. On the image upload page, it got stuck and showed the images as "pending", saying "No existing case matched this. Create one from what was uploaded." This contradicts the actual (correct) outcome, which was the images both being assosciated to the case. See uploadoutcome-test1.png
2. On the page, it had an option to enter registration and create a vehicle image case. Upon entering the correct registration, it proceeded to assosciate the image containing a registration with the correct case, and for the image with no registration, the page said that it created an image-initated case containing the image with no registration. There is no such case showing, and this does not appear to have been created. Given that the two images were attempted to be split, this suggests a regression from previous fixes that classify one set upload as all assosciated and one batch and should be investigated.
3. The queues page also shows an additional case in the total for "Not Ready" (total of 3), whilst only containing 2 actual "Not Ready" cases. Dashboard also shows 3.
4. Box folder for image initiated case was created and holds both images.



**Overall outcome:** Mostly succesful in testing functionality. Primary area of focus for fixes and remediations: post-upload page and functions.









###### **Test 2**



**Case ID**:



**Test Goal**: Reverse of Test 1. Prove that image cases will automatically merge into a matching instruction case, upon that instruction cases creation, assuming requirements and caveats met.



**Test Setup and Requirements:**



1. Images uploaded (manual upload / automation actor route) first.
2. Instructions forwarded after image initiated case created.
3. Instruction contains all required details for engineer hand-off.



**Expected outcome**: Image case automatically meged into instructions case.



**Outcome Breakdown:**



1. Image case created.
2. Image case held in "Not Ready" lacking instructions.
3. Upon e-mail receipt of instruction, Instruction case created.
4. Image case automatically merged into instruction case on upload.
5. If all other details are extracted and populated from instruction, instruction case in "Review" queue.
6. Image case closed as merged and superseded into instructions.
7. Former image case appears in instruction case history as merged / linked



**Actual outcome**:

Instruction case created. Image initiated case was merged into the instruction case succesfully.

**Issues:** 



1. Same as test 1 - incorrect totals showing for "Not Ready" cases - the previous image initiated case is still being classed as "active" despite being subsumed into the instructions initiated case.
2. Instruction case box folder does not contain the images. Image initiated box folder still exists. What should happen: Image initiated box folder is merged into instructions box folder.



**Overall outcome:** More succesful than test 1's area. Primary area of focus for remediation: Box folder management, Correct case status/cycle after case merge







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

