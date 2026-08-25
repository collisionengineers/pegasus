**Test 1**



**Case ID:**



**Test Goal:** Prove that uploaded images automatically match to a corresponding pre-existing instruction case, assuming requirements and caveats met.



**Test Setup and Requirements:**



1. Instruction e-mail sent, no images in e-mail.
2. No image initiated case pre-existing on system that matches.
3. Images uploaded (manual upload / automation actor route) with matching registration to the instructions, after instruction case is auto-created.



Two images were used on this test: 1 contained a full view of vehicle registration, the other contained no viewable registration (same vehicle).



**Expected outcome:** Images automatically matched to existing instructions case. No image initiated case created, as the images are automatically matched to instruction, so this is not required.



**Outcome Breakdown:**



1. Instruction case created.
2. Images match to case.   
3. No image initiated case created. 
4. Instruction case lands in "Not Ready" upon creation.
5. After image receipt and match, case moves to "Review".



**Actual outcome:**



Instruction case created. Used manual upload for images. Image case was not created, and the images were succesfully matched to the case. 



**Issues:** 



On the image upload page, it got stuck and showed the images as "pending", saying "No existing case matched this. Create one from what was uploaded." This contradicts the actual (correct) outcome, which was the images both being assosciated to the case. See uploadoutcome-test1.png



On the page, it had an option to enter registration and create a vehicle image case. Upon entering the correct registration, it proceeded to assosciate the image containing a registration with the correct case, and for the image with no registration, the page said that it created an image-initated case containing the image with no registration. There is no such case showing, and this does not appear to have been created. Given that the two images were attempted to be split, this suggests a regression from previous fixes that classify one set upload as all assosciated and one batch and should be investigated.



The queues page also shows an additional case in the total for "Not Ready" (total of 3), whilst only containing 2 actual "Not Ready" cases. Dashboard also shows 3.

Box folder for image initiated case was created and holds both images.



**Overall outcome:** Mostly succesful in testing functionality. Primary area of focus for fixes and remediations: post-upload page and functions.









**Test 2**



**Case ID:**



**Test Goal:** Reverse of Test 1. Prove that image cases will automatically merge into a matching instruction case, upon that instruction cases creation, assuming requirements and caveats met.



**Test Setup and Requirements:**



Images uploaded (manual upload / automation actor route) first.

Instructions forwarded after image initiated case created.

Instruction contains all required details for engineer hand-off.



**Expected outcome:** Image case automatically merged into instructions case.



**Outcome Breakdown:**



1. Image case created.
2. Image case held in "Not Ready" lacking instructions.
3. Upon e-mail receipt of instruction, Instruction case created.
4. Image case automatically merged into instruction case on upload. 
5. If all other details are extracted and populated from instruction, instruction case in "Review" queue.
6. Image case closed as merged and superseded into instructions.
7. Former image case appears in instruction case history as merged / linked



**Actual outcome:**



Instruction case created. Image initiated case was merged into the instruction case succesfully.



**Issues:** 



Same as test 1 - incorrect totals showing for "Not Ready" cases - the previous image initiated case is still being classed as "active" despite being subsumed into the instructions initiated case.



Instruction case box folder does not contain the images. Image initiated box folder still exists. What should happen: Image initiated box folder is merged into instructions box folder.



**Overall outcome:** More succesful than test 1's area. Primary area of focus for remediation: Box folder management, Correct case status/cycle after case merge



