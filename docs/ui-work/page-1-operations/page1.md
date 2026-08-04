Page 1 (titled "Operations") issues:



"Every current queue for the office, with the exact filter behind each count." - This is "narrating" a page.



The page is too cluttered with lots of containers and stats that are unnecessary.

This should be a general "dashboard" that should show:

1. Cases sent to engineer - day/week totals

2\. Engineer reports sent - day/week totals

3\. Add to dashboard: "New cases today: " "New cases today: " - should contain daily 

3\. Todos - For engineer type account only, this is assigned reports and e-mails/queries. 
4. Seperate case and intake queues into two. Rename intake queues to "e-mail activity". Rename "Case queues" to "Active cases"



E-mail activity totals: 

1. Received today

2\. Queries outstanding

3\. Needs sorting 



No need for combined e-mails received and sent count.



"Mailbox outocmes and owned retries. No dashboard aggregate exists for this route". - This is dev speak and narrating the UI - remove



Not ready and held showing as unavailable instead of 0.



Active cases:

1. Not Ready

2\. Review

3\. Held


"Staged intake artifacts" - This sounds like dev-speak leaking into front-end. Also showing a bug / error of some kind. This container does not belong on the dashboard. It also shows "Bounded inventory from the latest refresh: 0 pending · 1 failed · 0 orphaned · 0 unmatched" - this is not user facing language.



UI Wide rule - Should never show filesizes in bytes, always megabytes. (if appropriate - e.g. e-mail attachments) otherwise no reason to show these



"Requests: Box and Pegasus" - Box File Requests already superseded. No need to show Pegasus file requests totals on Dashboard.



Refresh and Last updated (see refresh-and-last-updated.png and page1.png for visual) - this should not be a large page-wide container, but something small, and compact, in a corner. The "current" icon is unnecessary.



















