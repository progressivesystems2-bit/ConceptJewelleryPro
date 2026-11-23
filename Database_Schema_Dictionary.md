# Complete Database Dictionary (from jydata.DBC)
Total Tables: 70

## Table: accmast (ID: 46142, Total Columns: 185)
Columns: acno, prefix, cname, groupid, pname, add1, add2, add3, city, location, phone, mobile, dob, asary, stno, pan, percent, crfine1, crfine2, cramt, del, active, crdays, igroupid, email, pin, stopdate, shortname, kprefix, kmno, kvmno, salary, pf, pfacno, esi, esiacno, no12, photo, quarter1, quarter2, quarter3, quarter4, tdsbill, wtbal, lbron, country, dep, nature, bankacno, leave, joindate, dept, sale, purchase, ord, repair, kitty, advance, issue, receive, taging, jobid, dless, sless, tran, krefby, ksman, kremarks, kinsamt, kmonths, kinsrec, ksdate, kedate, refgroup, stampid, paydate, bankchg, brpath, acno12, refby, kpmonth, scode, krefmno, bankbadla, bankex, metal, loginid, dtime, ballow, vtds, pvou, intr, comint, comintdef, grtran, mint, holiday, dir, khome, acnarr, doc, bktrc, orgacno, compath, compno, vcom, siteid, mcurrid, mcxcomex, rscomm, remarks, agentacno, hpname, odlimit, dep2, occupation, sflag, bankchg2, ktacno, scbony, bankno, swcode, nostro, minlbr, salaryb, prnrow, maxvtgno, main, add4, othchg, gender, spouse, spousedob, married, education, knowledge, potential, oppoints, website, cstno, rbookday, kinswt, branchid, uidno, wrkhrs, lottbal, othfine, gstin, stcode, stateid, gst, hsncode, comps, ecom, exptype, oploan, pfsalary, salins, cdc, opwithdraw, netbank, userid, corpid, accno, ifsc, urnid, bnkstatus, bnkalias, sysid, impcol, spemail, spmobile, upd, hisab, salarydet, stcatid, tcs, hmno, wstgon, dtswid, msme, mdtime, webid, altm, occode

## Table: acrep1 (ID: 53177, Total Columns: 38)
Columns: tdate, acno, opfine1, opfine2, opamount, clfine1, clfine2, clamount, opgrosswt1, opgrosswt2, opdiawt, opstnwt, clgrosswt1, clgrosswt2, cldiawt, clstnwt, reptype, ino, optag, opgwt, cltag, clgwt, vono, stampid, tunch, batch, lottid, repname, isfine1, isfine2, isamount, recfine1, recfine2, recamount, isdiawt, recdiawt, daily, comino

## Table: actax (ID: 46328, Total Columns: 6)
Columns: actaxid, frdate, todate, taxtype, percent, acno

## Table: actcsh1 (ID: 53216, Total Columns: 8)
Columns: tdate, clbal, tno, dmtno, clfine1, clfine2, loginid, dtime

## Table: attend1 (ID: 53225, Total Columns: 37)
Columns: acno, tdate, intime, outtime, status, othrs, penhrs, hrs, no12, late, uniform, holiday, working, actin, actout, timein, timeout, pendays, daily, penin, insout, dsalary, penflg, insflg, pintime, iouttime, remarks, in1, out1, in2, out2, cin1, cout1, cin2, cout2, wrkmin, lunchmin

## Table: badla1 (ID: 46335, Total Columns: 28)
Columns: badlaid, acno, tdate, narr, qty, badla, type, metaltype, loginid, dtime, bullwt, limitwt, daily, cduty, siteid, adjqty, duedate, bullid, cashbank, ino, perduty, totduty, dutyper, indentno, indentqty, estatus, edtime, audit

## Table: barnum1 (ID: 46364, Total Columns: 20)
Columns: barid, badlaid, refno, barno, wt, tno, vono, bdate, tdate, bullid, palletno, remarks, linkbarid, field1, field2, impdate, adjwt, jamatno, jamavono, daily

## Table: bb1 (ID: 46385, Total Columns: 39)
Columns: bbid, vono, tno, type, acno, tdate, ddate, refno, amount, narr, no12, smacno, linkbbid, itrnid, wt, stampid, tunch, fine1, fine2, main, stock, grossfine1, grossfine2, ino, unitid, daily, trbbid, oldacno, cpname, purwt, upflg, insflg, lnarr, comacno, cgst, igst, sgst, rnameid, remarks

## Table: bull1 (ID: 46425, Total Columns: 69)
Columns: bullid, tdate, acno, qty, lotts, rate, type, vtype, exrate, intorate, duty, pstax, stax, pcdc, cdcharge, exrateok, deldate, brate, badla, amount, loginid, dtime, metaltype, narr, badlawt, badlaid, ino, qtytrn, tno, limitid, bankbadla, bankex, vono, trnid, no12, billdate, refno, stampid, daily, billno, mcurrid, siteid, adjqty, advper, wotduty, upflg, insflg, cashdis, commsn, cashbank, scommsn, gst, sgst, cgst, seriesid, igst, gramt, grrate, tunch, perduty, tcs, mcxdet, tcsincl, strfdate, tds, billtno, audit, pmcxbadla, mcxbadla

## Table: ccsale1 (ID: 46495, Total Columns: 35)
Columns: csaleid, tdate, seriesid, noseriesid, billno, refno, total, tvalue, tax, ptax, memid, cpname, cpadd1, cpadd2, cpadd3, cpcity, cpphone, cpmobile, ino, gwt, rate, sacno, taxacno, incl, roff, siteid, wt, remarks, psgst, pcgst, sgst, cgst, gst, daily, stampid

## Table: cheques (ID: 46531, Total Columns: 16)
Columns: chqno, cname, bank, tdate, idate, amount, narr, acpay, sno, pan, chqid, flat, chbid, acno, bacno, tno

## Table: chq1 (ID: 53263, Total Columns: 7)
Columns: acno, fchqno, tchqno, chbid, lnarr, tdate, maxchqno

## Table: clstk1 (ID: 53271, Total Columns: 11)
Columns: ino, stampid, rate, igroupid, studed, total, tdate, batch, siteid, acno, valtype

## Table: comex1 (ID: 53283, Total Columns: 13)
Columns: tdate, dtime, acno, qty, rate, amount, dollar, vtype, narr, vono, hisab, flag, loginid

## Table: ctran1 (ID: 46548, Total Columns: 8)
Columns: cpid, trnid, amount, refno, tdate, mhisab, mpoint, memid

## Table: desicnm (ID: 46557, Total Columns: 4)
Columns: cdetid, designid, acno, cdesign

## Table: desidet (ID: 46562, Total Columns: 27)
Columns: detid, designid, ino, remark, wt, pc, rate, unitid, stampid, sizeid, sizewt, total, waxno, waxwt, waxpc, colour, clarity, lbr, lcode, lamt, setting, part, less, shape, rateon, acno, sdesign

## Table: design (ID: 46590, Total Columns: 54)
Columns: designid, design, ino, gwt, diawt, stnwt, othwt, photo, lbr, lbr2, mrate, mrp, wt, beeds, addwt, karigar, remarks, bamt, pc, tsno, collect, remarks2, remarks3, linkdesign, tdate, diapc, stnpc, lamt, mamt, damt, samt, othamt, photopath, pre, stampid, waxno, rem1, rem2, rem3, rem4, gwt1, gwt2, gwt3, isize, desigwt, status, dtime, loginid, others, waxwt, calmrp, vtgno, invtno, gwt4

## Table: didet1 (ID: 46645, Total Columns: 27)
Columns: idetid, itrnid, remarks, qty, type, rate, total, wt, pcwt, vono, less, tno, gwt, stampid, unitid, linkitrnid, tunch, ino, tdate, daily, trnid, bullid, pc, adjqty, upflg, insflg, billnarr

## Table: ditrn1 (ID: 46673, Total Columns: 241)
Columns: itrnid, trnid, vono, tno, itstdid, sntrnid, tdate, ino, remarks, gwt, wt, lesswt, pc, rate, tunch, wstg, mamt, type, stock, unitid, stampid, siteid, tvalue, studed, damt, samt, lamt, bamt, others, mrp, lbr, slbr, slbr2, slbr3, lcode, ptax, design, colour, clarity, shape, size, fine1, fine2, grosswt1, grosswt2, tgno, vtgno, tpre, tsno, pdis, costamt, diawt, stnwt, divide, prewstg, prewstgwt, polish, polishwt, orgrate, orggwt, orgtotal, dless, sless, total, kacno, karigar, no12, batch, goldwt, silwt, platwt, othwt, metalwt, jobno, jobstart, retwt, retpc, brokwt, brokpc, adjwt, adjvalue, balwt, balpc, sprice, mrate, dtime, setno, pktwt, pktless, gwt1, gwt2, part, purtunch, beeds, sbeeds, rateunit, beedsrate, banfine, sample, mettype, hisab, svalue, tunchwt, actunch, rptunch, trvono, trid, tunchrem, tchtno, photopath, diapc, stnpc, approval, certno, slbr2pc1, slbr2pc2, linktgno, linktsno, stdfine1, stdfine2, quality, hm, linkitrnid, pexwt, exwt, stunch, swstg, swt, profit, less, spolish, spolishwt, tinto, sothers, allot, tgdid, pless, grossfine1, grossfine2, iwt, ipc, calcwt, calcpc, ritrnid, bbid, linkbbid, pretwt, lottid, sizeid, owner, ownacno, trtno, labid, deldate, salemrp, jobid, stkfine1, stkfine2, otgdid, ktino, kttgno, ktvtgno, ktslbr2, ktsothers, kttsno, ktitrnid, sttunch, billtype, stdlbr, costrate, ojobno, ojobid, ocancel, ocremarks, ocdate, ocloginid, octime, designid, costvalue, costmamt, costdamt, costsamt, costlamt, costwstg, dis, srtype, costtotal, goldamt, silamt, tagfine1, tagfine2, purchase, pcrate, setnobal, jobgwt, orgwt, othrem, revised, saleless, pprofit, lbradd, daily, diawt1, diawt2, stnwt1, stnwt2, lakhwt, sbanfine, upflg, wid, oacno, oamt, poamt, khisab, mrp1, mrp2, costing1, costing2, witno, lbron, dollar, dolxrate, kisno, diaremark, lamtidet, distype, ihisab, smacno, psgst, pcgst, pigst, sgst, cgst, igst, tgcode, lnkjobid, ktbatch, ttvalue, ksitrnid, ktsvalue, xtfld

## Table: dmitrn1 (ID: 53297, Total Columns: 245)
Columns: trnid, vono, tno, itrnid, itstdid, sntrnid, tdate, ino, remarks, gwt, wt, lesswt, pc, rate, tunch, wstg, mamt, type, stock, unitid, stampid, siteid, tvalue, studed, damt, samt, lamt, bamt, others, mrp, lbr, slbr, slbr2, slbr3, lcode, ptax, design, colour, clarity, shape, size, fine1, fine2, grosswt1, grosswt2, tgno, vtgno, tpre, tsno, pdis, costamt, diawt, stnwt, divide, prewstg, prewstgwt, polish, polishwt, orgrate, orggwt, orgtotal, dless, sless, total, kacno, karigar, no12, batch, goldwt, silwt, platwt, othwt, metalwt, jobno, jobstart, retwt, retpc, brokwt, brokpc, adjwt, adjvalue, balwt, balpc, sprice, mrate, dtime, setno, pktwt, pktless, gwt1, gwt2, part, purtunch, beeds, sbeeds, rateunit, beedsrate, banfine, sample, mettype, hisab, svalue, tunchwt, actunch, rptunch, trvono, trid, tunchrem, tchtno, photopath, diapc, stnpc, approval, certno, slbr2pc1, slbr2pc2, linktgno, linktsno, stdfine1, stdfine2, quality, hm, linkitrnid, pexwt, exwt, stunch, swstg, swt, profit, less, spolish, spolishwt, tinto, sothers, allot, tgdid, pless, grossfine1, grossfine2, iwt, ipc, calcwt, calcpc, ritrnid, bbid, linkbbid, pretwt, lottid, sizeid, owner, ownacno, bitrnid, apitrnid, apritrnid, trtno, labid, deldate, salemrp, jobid, stkfine1, stkfine2, conitrnid, otgdid, ktino, kttgno, ktvtgno, ktslbr2, ktsothers, kttsno, ktitrnid, sttunch, billtype, stdlbr, costrate, ojobno, ojobid, ocancel, ocremarks, ocdate, ocloginid, octime, designid, costvalue, costmamt, costdamt, costsamt, costlamt, costwstg, dis, srtype, costtotal, goldamt, silamt, tagfine1, tagfine2, purchase, pcrate, setnobal, jobgwt, lbrwt, othrem, revised, saleless, pprofit, lbradd, sitstdid, daily, diawt1, diawt2, stnwt1, stnwt2, lakhwt, sbanfine, upflg, wid, oacno, oamt, poamt, khisab, mrp1, mrp2, costing1, costing2, witno, lbron, dollar, dolxrate, kisno, diaremark, lamtidet, distype, dmtno, smacno, assort, psgst, pcgst, pigst, sgst, cgst, igst, tgcode, lnkjobid, ktbatch, ksitrnid, xtfld

## Table: dmsnd1 (ID: 53543, Total Columns: 34)
Columns: sntrnid, tno, sundryid, vono, adjsale, adjacno, addless, fed, fedp, tax, tdate, vtypecode, fine1, fine2, amount, percent, narr, wt, wtunch, rmode, rate, rtunch, name, badlarate, badla, badlaon, badlanj, rpamount, btype, banksno, bankchg, dep, svou, dmtno

## Table: dmtran1 (ID: 46915, Total Columns: 253)
Columns: trnid, tno, acno, tdate, vtypeid, vono, fine1, fine2, amount, lnarr, narr, billno, refno, seriesid, noseriesid, samount, tax, discount, adjust, diff, siteid, siteidto, account, smacno, op1, op2, op3, op4, op5, op6, no12, rnameid, grosswt1, grosswt2, cpname, cpadd1, cpadd2, cpadd3, cplocation, cpcity, cpphone, cpmobile, cpdob, cpasary, cpid, ptax, surcharge, vtypecode, hisab, loginid, approved, vtcode, ddate, status, idate, kittyins, bhavwt, bhavtunch, bhavrate, bhavrtunch, dless, sless, lbron, hisabvono, vtype, memid, dtime, grossfine1, grossfine2, badlarate, badla, badlaon, badlanj, final, deliver, rmode, mainacno, lottid, ordid, stampid, issgwt, isswt, recgwt, recwt, losswt, lossfine, balwt, balfine, exwt, exfine, lbramt, khisab, stnwt, stnpc, diawt, diapc, stbeeds, alwstg, alfine, dustwt, tunchwt, isspc, recpc, avgtunch, lossdate, waxdiawt, waxstwt, stfine, extunch, meltunch, melwt, kadiwt, kpcwt, kadipc, konewt, bdscalc, ststnwt, ststnpc, flag, ratefix, crdays, rpamount, qttno, invdate, ebill, audit, chqid, bbid, billtype, daily, altvou, mhisab, billfine1, billfine2, billbal, adjfine1, adjfine2, adjbal, tgino, tgigroupid, tgptunch, tgpwstg, tgrate, tgmrate, tgspolish, tgstunch, tglbr, tgslbr, tgstkino, tgstampid, tgpcless, tgdsless, tgvalless, pamount, upflg, trdata, mainrecord, pdc, insflg, jobid, uphisab, oacno, jobno, ojobno, ojobid, designid, issfine, recfine, polosswt, polossfine, postnfine, bullid, tgppolish, mrnameid, exptax, acno12acno, exrateok, cdcharge, stax, dollar, metaltype, chqtrnid, inthisab, rtunch, wtunch, bankchg, postamt, postrate, postlbr, sumno, spolishwt, dustfine, orgacno, ino, trtrnid, tgset, orgcname, ledwt, opening, macno, tgbatch, tunchfine, kittyvono, postlfine, mcurrid, mcamount, xrate, mcxcno, appcode, cardtype, billnarr, badlaid, mcamount2, mcurrid1, mcurrid2, allot, fine3, fine4, op7, ugamount, ledgerflg, cntacno, prnflag, labid, dmtno, dmloginid, dmtime, del, comacno, comamt, mpoints, couponid, webid, mtranid, interest, notecntr, duedate, excise, schgamt, inthisabg, inthisabs, cppan, othfine, cashbank, cgst, sgst, igst, cpstateid, cpgstin, brid, irn, billinfo, itcstatus

## Table: dstrn1 (ID: 47169, Total Columns: 67)
Columns: sntrnid, tno, sundryid, vono, adjsale, adjacno, addless, fed, fedp, tax, tdate, vtypecode, fine1, fine2, amount, percent, narr, wt, wtunch, rmode, rate, rtunch, name, badlarate, badla, badlaon, badlanj, rpamount, btype, banksno, bankchg, dep, svou, daily, upflg, stampid, bullid, ratediff, batch, appcode, cardtype, expdate, fine3, fine4, remarks, oldacno, othacno, agent, advper, bctype, insflg, rnameid, badlaid, couponid, intorate, othfine, sgst, cgst, igst, cashbank, ktid, kttrnid, gwt, defamt, notecntr, tdsinfo, tridbank

## Table: dtinfo (ID: 47237, Total Columns: 5)
Columns: dtifid, idesc, ino, dtype, dtswid

## Table: dtrn1 (ID: 47243, Total Columns: 253)
Columns: trnid, tno, acno, tdate, vtypeid, vono, fine1, fine2, amount, lnarr, narr, billno, refno, seriesid, noseriesid, samount, tax, discount, adjust, diff, siteid, siteidto, account, smacno, op1, op2, op3, op4, op5, op6, no12, rnameid, grosswt1, grosswt2, cpname, cpadd1, cpadd2, cpadd3, cplocation, cpcity, cpphone, cpmobile, cpdob, cpasary, cpid, ptax, surcharge, vtypecode, hisab, loginid, approved, vtcode, ddate, status, idate, kittyins, bhavwt, bhavtunch, bhavrate, bhavrtunch, dless, sless, lbron, hisabvono, vtype, memid, dtime, grossfine1, grossfine2, badlarate, badla, badlaon, badlanj, final, deliver, rmode, mainacno, lottid, ordid, stampid, issgwt, isswt, recgwt, recwt, losswt, lossfine, balwt, balfine, exwt, exfine, lbramt, khisab, stnwt, stnpc, diawt, diapc, stbeeds, alwstg, alfine, dustwt, tunchwt, isspc, recpc, avgtunch, lossdate, waxdiawt, waxstwt, stfine, extunch, meltunch, melwt, kadiwt, kpcwt, kadipc, konewt, bdscalc, ststnwt, ststnpc, flag, ratefix, crdays, rpamount, qttno, invdate, ebill, audit, chqid, bbid, billtype, daily, altvou, mhisab, billfine1, billfine2, billbal, adjfine1, adjfine2, adjbal, tgino, tgigroupid, tgptunch, tgpwstg, tgrate, tgmrate, tgspolish, tgstunch, tglbr, tgslbr, tgstkino, tgstampid, tgpcless, tgdsless, tgvalless, pamount, upflg, trdata, mainrecord, pdc, insflg, jobid, uphisab, oacno, jobno, ojobno, ojobid, designid, issfine, recfine, polosswt, polossfine, postnfine, bullid, tgppolish, mrnameid, exptax, acno12acno, exrateok, cdcharge, stax, dollar, metaltype, chqtrnid, inthisab, rtunch, wtunch, bankchg, postamt, postrate, postlbr, sumno, spolishwt, dustfine, orgacno, ino, trtrnid, tgset, orgcname, ledwt, opening, macno, tgbatch, tunchfine, kittyvono, postlfine, mcurrid, mcamount, xrate, mcxcno, appcode, cardtype, billnarr, badlaid, mcamount2, mcurrid1, mcurrid2, allot, fine3, fine4, op7, ugamount, ledgerflg, cntacno, prnflag, labid, dmtno, dmloginid, dmtime, del, comacno, comamt, mpoints, couponid, webid, mtranid, interest, notecntr, duedate, excise, schgamt, inthisabg, inthisabs, cppan, othfine, cashbank, cgst, sgst, igst, cpstateid, cpgstin, brid, irn, billinfo, itcstatus

## Table: gisdet1 (ID: 47497, Total Columns: 16)
Columns: gisdetid, grade, items, pcdet, remarks, gwt, lesswt, wt, tunch, fine, rate, total, gisid, daily, photopath, hremarks

## Table: gissue1 (ID: 47514, Total Columns: 77)
Columns: gisid, rnameid, sno, refno, tdate, oldrefno, oldgisid, goldgwt, goldwt, goldless, goldtunch, goldfine, goldrate, goldtotal, silgwt, silwt, silless, siltunch, silfine, silrate, siltotal, approx, amount, amount1, amount2, months, intr, intr2, mode, bacno, chqno, location, narr, no12, gtype, seriesid, noseriesid, flag, loginid, daily, other, ramt1, ramt2, rint1, rint2, gtisid, gtrcid, dtime, acno, trdata, freemark, delmark, tno, dis1, dis2, soldmark, gcomint, audit, remarks, remdate, itemphoto, gisrint, prerefno, trgisid, ggisid, intperiod, siteid, cattype, docimg, smacno, curint1, curint2, period1, period2, curdate, cash, cash2

## Table: grcdet1 (ID: 53578, Total Columns: 14)
Columns: grcpid, grcptid, cgisid, cgrcptid, tdate, flag, amount1, amount2, int1, int2, daily, dis1, dis2, gisidnew

## Table: grcpt1 (ID: 47592, Total Columns: 43)
Columns: grcptid, sno, refno, rnameid, amount, amount1, amount2, bacno, int, int1, int2, narr, flag, gtype, intr, tdate, mode, chqno, seriesid, noseriesid, no12, loginid, daily, gisid, dtime, acno, trdata, tno, grcpid, dis, dis1, dis2, audit, gisidnew, trgrcpid, trgrcptid, remdate, siteid, remarks, docimg, smacno, cash, cash2

## Table: gstr1 (ID: 53593, Total Columns: 11)
Columns: monthyy, tdate, gstin, refno, taxableamt, pigst, igst, sgst, cgst, invamt, cess

## Table: gtis1 (ID: 47636, Total Columns: 38)
Columns: gtisid, rnameid, sno, refno, tdate, approx, amount, months, intr, mode, bacno, chqno, location, narr, no12, gtype, seriesid, noseriesid, flag, loginid, daily, other, dtime, acno, oldgtisid, oldrefno, ramt, rint, tno, audit, delmark, intperiod, siteid, prerefno, curint, period, curdate, remdate

## Table: gtisd1 (ID: 53605, Total Columns: 7)
Columns: gtisid, gisid, tdate, amount, gtisdid, daily, approx

## Table: gtran1 (ID: 53613, Total Columns: 8)
Columns: grcpid, gisidold, gisidnew, gtisid, grcptid, gtrcid, cgisidnew, gisid

## Table: gtrc1 (ID: 47675, Total Columns: 29)
Columns: gtrcid, rnameid, sno, refno, tdate, approx, amount, months, intr, mode, bacno, chqno, location, narr, no12, gtype, seriesid, noseriesid, flag, loginid, daily, other, int, dtime, acno, tno, dis, audit, siteid

## Table: gtrcd1 (ID: 53622, Total Columns: 8)
Columns: gtrcid, cgtisid, cgtrcid, tdate, flag, daily, amount, int

## Table: idet1 (ID: 47705, Total Columns: 27)
Columns: idetid, itrnid, remarks, qty, type, rate, total, wt, pcwt, vono, less, tno, gwt, stampid, unitid, linkitrnid, tunch, ino, tdate, daily, trnid, bullid, pc, adjqty, upflg, insflg, billnarr

## Table: itemmast (ID: 47733, Total Columns: 126)
Columns: ino, idesc, diast, igroupid, pname, unitid, sm, tpre, pc, treypc, maxtgno, starttgno, pcstock, blist, addwt, twodet, sale, purchase, karigar, stock, stval, studed, stampid, gcodeid, finecal, cstamp, cbeeds, cgwt, grade, catid, restgwt, resttunch, restwstg, shortname, boxwt, polish, profit, intorate, desipre, wswstg, wslbr, retwstg, retlbr, lcode, deftunch, stones, linktg, linkino, stmethod, srno, gcreate, bpretype, istud, badhel, ditemrate, restrate, stvaltunch, stvalwstg, stvallbr, irgwt, tpcode, partydst, restzero, gcomm, gcon, gcdis, scomm, scon, scdis, vstk, tgdigit, desidigit, rem1, rem2, loginid, dtime, lbron, slbron, dbname, design, mrp, raparate, saleless, reststamp, boxpc, pcrate, restbatch, sqrmtr, pexwt, restoth, tagwt, hpname, restwl, shortcnm, restpc, scowt, scors, bintorate, polywt, wirewt, lstamp, prncmd, mpoints, prnsno, tgstkless, dtrnstk, lessdet, mino, msizeid, pointupon, tglbradd, ithsncode, srateunit, svstk, minstk, insamt, webdis, webidesc, webcat, webconf, barslist, chgitm, itmconf, snobcode, minmg, altm

## Table: itran1 (ID: 47860, Total Columns: 241)
Columns: itrnid, trnid, vono, tno, itstdid, sntrnid, tdate, ino, remarks, gwt, wt, lesswt, pc, rate, tunch, wstg, mamt, type, stock, unitid, stampid, siteid, tvalue, studed, damt, samt, lamt, bamt, others, mrp, lbr, slbr, slbr2, slbr3, lcode, ptax, design, colour, clarity, shape, size, fine1, fine2, grosswt1, grosswt2, tgno, vtgno, tpre, tsno, pdis, costamt, diawt, stnwt, divide, prewstg, prewstgwt, polish, polishwt, orgrate, orggwt, orgtotal, dless, sless, total, kacno, karigar, no12, batch, goldwt, silwt, platwt, othwt, metalwt, jobno, jobstart, retwt, retpc, brokwt, brokpc, adjwt, adjvalue, balwt, balpc, sprice, mrate, dtime, setno, pktwt, pktless, gwt1, gwt2, part, purtunch, beeds, sbeeds, rateunit, beedsrate, banfine, sample, mettype, hisab, svalue, tunchwt, actunch, rptunch, trvono, trid, tunchrem, tchtno, photopath, diapc, stnpc, approval, certno, slbr2pc1, slbr2pc2, linktgno, linktsno, stdfine1, stdfine2, quality, hm, linkitrnid, pexwt, exwt, stunch, swstg, swt, profit, less, spolish, spolishwt, tinto, sothers, allot, tgdid, pless, grossfine1, grossfine2, iwt, ipc, calcwt, calcpc, ritrnid, bbid, linkbbid, pretwt, lottid, sizeid, owner, ownacno, trtno, labid, deldate, salemrp, jobid, stkfine1, stkfine2, otgdid, ktino, kttgno, ktvtgno, ktslbr2, ktsothers, kttsno, ktitrnid, sttunch, billtype, stdlbr, costrate, ojobno, ojobid, ocancel, ocremarks, ocdate, ocloginid, octime, designid, costvalue, costmamt, costdamt, costsamt, costlamt, costwstg, dis, srtype, costtotal, goldamt, silamt, tagfine1, tagfine2, purchase, pcrate, setnobal, jobgwt, orgwt, othrem, revised, saleless, pprofit, lbradd, daily, diawt1, diawt2, stnwt1, stnwt2, lakhwt, sbanfine, upflg, wid, oacno, oamt, poamt, khisab, mrp1, mrp2, costing1, costing2, witno, lbron, dollar, dolxrate, kisno, diaremark, lamtidet, distype, ihisab, smacno, psgst, pcgst, pigst, sgst, cgst, igst, tgcode, lnkjobid, ktbatch, ttvalue, ksitrnid, ktsvalue, xtfld

## Table: itrndel1 (ID: 48102, Total Columns: 8)
Columns: delid, dtime, itrnid, trnid, gwt, pc, tno, loginid

## Table: itstd1 (ID: 48111, Total Columns: 238)
Columns: itrnid, trnid, vono, tno, itstdid, sntrnid, tdate, ino, remarks, gwt, wt, lesswt, pc, rate, tunch, wstg, mamt, type, stock, unitid, stampid, siteid, tvalue, studed, damt, samt, lamt, bamt, others, mrp, lbr, slbr, slbr2, slbr3, lcode, ptax, design, colour, clarity, shape, size, fine1, fine2, grosswt1, grosswt2, tgno, vtgno, tpre, tsno, pdis, costamt, diawt, stnwt, divide, prewstg, prewstgwt, polish, polishwt, orgrate, orggwt, orgtotal, dless, sless, total, kacno, karigar, no12, batch, goldwt, silwt, platwt, othwt, metalwt, jobno, jobstart, retwt, retpc, brokwt, brokpc, adjwt, adjvalue, balwt, balpc, sprice, mrate, dtime, setno, pktwt, pktless, gwt1, gwt2, part, purtunch, beeds, sbeeds, rateunit, beedsrate, banfine, sample, mettype, hisab, svalue, tunchwt, actunch, rptunch, trvono, trid, tunchrem, tchtno, photopath, diapc, stnpc, approval, certno, slbr2pc1, slbr2pc2, linktgno, linktsno, stdfine1, stdfine2, quality, hm, linkitrnid, pexwt, exwt, stunch, swstg, swt, profit, less, spolish, spolishwt, tinto, sothers, allot, tgdid, pless, grossfine1, grossfine2, iwt, ipc, calcwt, calcpc, ritrnid, bbid, linkbbid, pretwt, lottid, sizeid, owner, ownacno, trtno, labid, deldate, salemrp, jobid, stkfine1, stkfine2, otgdid, ktino, kttgno, ktvtgno, ktslbr2, ktsothers, kttsno, sttunch, billtype, stdlbr, costrate, ojobno, ojobid, ocancel, ocremarks, ocdate, ocloginid, octime, designid, costvalue, costmamt, costdamt, costsamt, costlamt, costwstg, dis, srtype, costtotal, goldamt, silamt, tagfine1, tagfine2, purchase, pcrate, setnobal, jobgwt, orgwt, othrem, revised, saleless, pprofit, lbradd, daily, diawt1, diawt2, stnwt1, stnwt2, lakhwt, sbanfine, upflg, wid, oacno, oamt, poamt, khisab, mrp1, mrp2, costing1, costing2, witno, lbron, dollar, dolxrate, kisno, diaremark, lamtidet, distype, ihisab, smacno, psgst, pcgst, pigst, sgst, cgst, igst, tgcode, lnkjobid, ktbatch, ttvalue, pcwt

## Table: jobmast1 (ID: 48350, Total Columns: 9)
Columns: jobid, jobno, vtgno, tno, tdate, ino, daily, type, tpre

## Table: kachi1 (ID: 48360, Total Columns: 28)
Columns: kchid, tno, itrnid, num, wt, tunch, fine, sitrnid, stno, swt, stunch, sfine, ino, flag, remark, fremark, siteid, testwt, testtunch, daily, acno, tdate, refno, lottid, carat, scarat, trkchid, testnum

## Table: limit1 (ID: 48389, Total Columns: 31)
Columns: limitid, tdate, acno, qty, lotts, rate, type, vtype, exrate, intorate, duty, pstax, stax, pcdc, cdcharge, exrateok, deldate, brate, badla, amount, loginid, dtime, metaltype, narr, badlawt, badlaid, ino, qtytrn, tno, bullid, saudawt

## Table: lott1 (ID: 48421, Total Columns: 26)
Columns: lottid, lottno, ldet, lott, mark, ltunch, lottdate, eftunch, consume, ino, ordid, pc, reject, remarks, tunch, itrnid, acno, tno, daily, gwt, stampid, siteid, quality, pdflag, design, designid

## Table: ltran1 (ID: 48448, Total Columns: 10)
Columns: labtrnid, tno, labid, wt, rate, amount, wstg, wstgwt, wstgfine, daily

## Table: mcx1 (ID: 48459, Total Columns: 13)
Columns: mcxid, tdate, ino, qty, acno, rate, amount, mcxcno, dtime, loginid, mcxinto, vono, tno

## Table: members (ID: 48473, Total Columns: 49)
Columns: cpid, cpname, cpadd1, cpadd2, cpadd3, cplocation, cpcity, cpmobile, cpphone, cpdob, cpasary, memid, cprelation, acno, issdate, cpemail, refby, ccomp, bcname, bacno, ifsccode, cptype, cppan, cppin, gender, spouse, spousedob, married, education, occupation, knowledge, potential, mobile, branchid, points, webid, spmobile, spemail, cpaadhar, cpstateid, cpgstin, mcpid, prefix, remark, bangle, ring, country, dtime, main

## Table: ord1 (ID: 48523, Total Columns: 23)
Columns: ordid, seriesid, noseriesid, billno, ordno, acno, tdate, ddate, reference, flag, pdflag, remarks, stampid, tunch, ebill, lottid, ordtype, daily, smacno, sold, branchid, dtime, loginid

## Table: orddet1 (ID: 53631, Total Columns: 13)
Columns: ordid, otrnid, ino, designid, pc, gwt, pcwt, remark, stampid, jobpc, daily, kacno, isize

## Table: ordrem1 (ID: 53645, Total Columns: 4)
Columns: remtype, remark, ordid, daily

## Table: ordtrn1 (ID: 48547, Total Columns: 13)
Columns: otrnid, ordid, ino, remark, wtrange, pc, gwt, daily, rate, rlid, stampid, duedate, linkordid

## Table: prndbk (ID: 48561, Total Columns: 14)
Columns: prndbkid, cprndbkid, sno, dbkhead, dbktype, dbkgrpby, dbkoby, dbkflt, dbkflton, ie, dbkname, dbksmon, fltsite, fltlogin

## Table: qidet1 (ID: 48576, Total Columns: 26)
Columns: idetid, itrnid, remarks, qty, type, rate, total, wt, pcwt, vono, less, tno, gwt, stampid, unitid, linkitrnid, tunch, ino, tdate, daily, trnid, bullid, pc, adjqty, upflg, insflg

## Table: qitran1 (ID: 48603, Total Columns: 239)
Columns: itrnid, trnid, vono, tno, itstdid, sntrnid, tdate, ino, remarks, gwt, wt, lesswt, pc, rate, tunch, wstg, mamt, type, stock, unitid, stampid, siteid, tvalue, studed, damt, samt, lamt, bamt, others, mrp, lbr, slbr, slbr2, slbr3, lcode, ptax, desino, design, colour, clarity, shape, size, fine1, fine2, grosswt1, grosswt2, tgno, vtgno, tpre, tsno, pdis, discount, diawt, stnwt, divide, prewstg, prewstgwt, polish, polishwt, orgrate, orggwt, orgtotal, dless, sless, oldstd, total, kacno, karigar, no12, batch, goldwt, silwt, platwt, othwt, metalwt, jobno, setino, jobstart, retwt, retpc, brokwt, brokpc, misswt, misspc, balwt, balpc, sprice, mrate, dtime, status, setno, pktwt, pktless, gwt1, gwt2, part, purtunch, beeds, sbeeds, rateunit, beedsrate, banfine, tunch1, tunch2, tunch3, sample, mettype, hisab, svalue, tunchwt, actunch, rptunch, trvono, trid, tunchrem, tunchstat, photopath, diapc, stnpc, approval, certno, slbr2pc1, slbr2pc2, linktgno, linktsno, stdfine1, stdfine2, quality, hm, linkitrnid, pexwt, exwt, stunch, swstg, swt, profit, less, spolish, spolishwt, tinto, sothers, allot, tgdid, pless, grossfine1, grossfine2, iwt, ipc, calcwt, calcpc, ritrnid, bbid, linkbbid, pretwt, lottid, sizeid, owner, ownacno, bitrnid, apitrnid, apritrnid, trtno, labid, deldate, salemrp, jobid, stkfine1, stkfine2, conitrnid, otgdid, ktino, kttgno, ktvtgno, ktslbr2, ktsothers, kttsno, ktitrnid, sttunch, billtype, stdlbr, costrate, ojobno, ojobid, ocancel, ocremarks, ocdate, ocloginid, octime, designid, costvalue, costmamt, costdamt, costsamt, costlamt, dis, srtype, costtotal, goldamt, silamt, tagfine1, tagfine2, purchase, pcrate, setnobal, jobgwt, lbrwt, othrem, revised, saleless, pprofit, lbradd, sitstdid, daily, altvou, diawt1, diawt2, stnwt1, stnwt2, lakhwt, sbanfine, upflg, wid, oacno, oamt, poamt, diaremark, dolxrate, dollar, costing1, sgst, cgst, igst, psgst, pcgst, pigst, smacno, tgcode, lamtidet, xtfld

## Table: qstrn1 (ID: 48843, Total Columns: 48)
Columns: sntrnid, tno, sundryid, vono, adjsale, adjacno, addless, fed, fedp, tax, tdate, vtypecode, fine1, fine2, amount, percent, narr, wt, wtunch, rmode, rate, rtunch, name, badlarate, badla, badlaon, badlanj, rpamount, btype, banksno, bankchg, dep, svou, daily, upflg, stampid, bullid, ratediff, batch, appcode, cardtype, expdate, fine3, fine4, remarks, oldacno, othacno, bctype

## Table: quot1 (ID: 48892, Total Columns: 134)
Columns: trnid, tno, acno, tdate, vtypeid, vono, fine1, fine2, amount, lnarr, narr, billno, refno, seriesid, noseriesid, samount, tax, discount, adjust, diff, siteid, siteidfrom, siteidto, account, smacno, op1, op2, op3, op4, op5, op6, no12, rnameid, grosswt1, grosswt2, cpname, cpadd1, cpadd2, cpadd3, cplocation, cpcity, cpphone, cpmobile, cpdob, cpasary, cpid, ptax, surcharge, vtypecode, hisab, loginid, approved, vtcode, ddate, status, idate, kittyins, bhavwt, bhavtunch, bhavrate, bhavrtunch, dless, sless, lbron, hisabvono, vtype, memid, dtime, grossfine1, grossfine2, badlarate, badla, badlaon, badlanj, final, deliver, rmode, mainacno, lottid, ordid, stampid, issgwt, isswt, recgwt, recwt, losswt, lossfine, balwt, balfine, exwt, exfine, lbramt, khisab, stnwt, stnpc, diawt, diapc, stbeeds, alwstg, alfine, dustwt, tunchwt, isspc, recpc, avgtunch, lossdate, waxdiawt, waxstwt, stfine, extunch, meltunch, melwt, kadiwt, kpcwt, kadipc, konewt, bdscalc, ststnwt, stdiawt, flag, ratefix, crdays, ebill, pamount, trntno, daily, op7, excise, schgamt, sgst, cgst, igst, invdate, tgbatch

## Table: saltrn1 (ID: 53650, Total Columns: 35)
Columns: mon, year, acno, days, basic, salary, ppf, esi, tds, no12, ppfcomp, esicomp, ballow, allowance, deduction, payable, pymode, bacno, tdate, paydate, leave, oldacno, dedloan, penin, insout, dsalary, exasal, expsal, lnarr, hra, othalw, spalw, lta, cbasic, salcash

## Table: smitrn1 (ID: 53686, Total Columns: 15)
Columns: acno, tdate, tsno, ino, pc, gwt, type, smitrnid, tgno, tpre, remarks, salemrp, diawt, tno, dtime

## Table: sndtrn1 (ID: 49027, Total Columns: 67)
Columns: sntrnid, tno, sundryid, vono, adjsale, adjacno, addless, fed, fedp, tax, tdate, vtypecode, fine1, fine2, amount, percent, narr, wt, wtunch, rmode, rate, rtunch, name, badlarate, badla, badlaon, badlanj, rpamount, btype, banksno, bankchg, dep, svou, daily, upflg, stampid, bullid, ratediff, batch, appcode, cardtype, expdate, fine3, fine4, remarks, oldacno, othacno, agent, advper, bctype, insflg, rnameid, badlaid, couponid, intorate, othfine, sgst, cgst, igst, cashbank, ktid, kttrnid, gwt, defamt, notecntr, tdsinfo, tridbank

## Table: tchreg1 (ID: 53702, Total Columns: 36)
Columns: lottdate, lott, lottid, soldto, remark, flag, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19, p20, p21, p22, p23, p24, p25, p26, p27, p28, p29, p30

## Table: tgd1 (ID: 49095, Total Columns: 65)
Columns: tgdid, tsno, itrnid, tno, ino, remarks, stampid, wt, pc, rate, unitid, print, sprice, rateunit, tunch, fine1, fine2, pexwt, exwt, part, less, colour, clarity, shape, size, batch, prewstg, stunch, lbr, lcode, slbr, others, pdis, total, itype, otgdid, costrate, saleless, svalue, costvalue, stkfine1, stkfine2, prewstgwt, mamt, lamt, daily, upinfo, siteid, sizeid, swstg, owner, wstg, dollar, dolxrate, sizeval, oamt, bamt, diaremark, certno, design, othrem, quality, dis, intorate, profit

## Table: tggen1 (ID: 53739, Total Columns: 94)
Columns: tsno, tgno, vtgno, ino, tpre, remarks, tdate, itrnid, tggenid, gwt, lesswt, wt, diawt, stnwt, goldwt, silwt, platwt, othwt, lbr, lbr2, lbr3, slbr, slbr2, slbr3, status, tunch, wstg, stunch, swstg, beeds, sbeeds, sothers, othrem, design, desino, karigar, mrate, crate, gwt1, gwt2, slbr2pc1, slbr2pc2, stampid, flag, photopath, size, category, colour, clarity, polish, polishwt, siteid, login, setno, ordno, dless, sless, linktgno, linktsno, addmrp, lamt, damt, samt, mamt, bamt, others, tagfine1, tagfine2, purmrp, purfine1, purfine2, puramt, costlbr, costlbr2, costlbr3, costlamt, costdamt, costsamt, costmamt, costothers, costmrp, mrp, hm, certno, spolish, spolishwt, swt, vono, tno, type, sdless, ssless, stdfine1, stdfine2

## Table: tgm1 (ID: 49161, Total Columns: 180)
Columns: tsno, tgno, vtgno, ino, tpre, remarks, tdate, itrnid, gwt, lesswt, wt, diawt, stnwt, goldwt, silwt, platwt, othwt, lbr, lbr2, lbr3, slbr, slbr2, slbr3, status, tunch, wstg, stunch, swstg, beeds, sbeeds, sothers, othrem, design, desino, kacno, karigar, mrate, costrate, gwt1, gwt2, slbr2pc1, slbr2pc2, stampid, flag, photopath, size, quality, colour, clarity, polish, polishwt, siteid, login, setno, ordno, dless, sless, linktgno, linktsno, addmrp, lamt, damt, samt, mamt, others, tagfine1, tagfine2, costdamt, costsamt, costmamt, mrp, hm, certno, spolish, spolishwt, swt, vono, tno, type, sdless, ssless, billtype, unitid, pc, salemrp, jobid, costtotal, costwstg, grossfine1, grossfine2, fine1, fine2, total, pcrate, pprofit, upflag, stkfine1, stkfine2, rate, daily, tralt, diawt1, diawt2, stnwt1, stnwt2, lakhwt, banfine, sbanfine, upstatus, upinfo, approval, designid, repwt, lastdate, diapc, stnpc, mrp1, mrp2, costing1, costing2, mflag, sizeid, gitrnid, diaremark, dollar, dolxrate, pdis, oldstatus, lcode, shape, tvalue, tritrnid, uptgno, svalue, rfid, batch, costlamt, ojobid, olinktgno, sdamt, ssamt, slamt, smamt, dis, pktwt, pktless, orggwt, orgtotal, oacno, vtotal, tgmidesc, sdiawt, sstnwt, trsiteid, sample, srtype, oamt, poamt, rlid, costamt, ocdate, trlbr, trlcode, trflag, tgcode, stagfine1, stagfine2, pless, calmrp, gst, saleless, csmamt, grade, cslamt, srate, deldate, pgst, xtfld, cstdmamt, sosamt

## Table: tran1 (ID: 49342, Total Columns: 253)
Columns: trnid, tno, acno, tdate, vtypeid, vono, fine1, fine2, amount, lnarr, narr, billno, refno, seriesid, noseriesid, samount, tax, discount, adjust, diff, siteid, siteidto, account, smacno, op1, op2, op3, op4, op5, op6, no12, rnameid, grosswt1, grosswt2, cpname, cpadd1, cpadd2, cpadd3, cplocation, cpcity, cpphone, cpmobile, cpdob, cpasary, cpid, ptax, surcharge, vtypecode, hisab, loginid, approved, vtcode, ddate, status, idate, kittyins, bhavwt, bhavtunch, bhavrate, bhavrtunch, dless, sless, lbron, hisabvono, vtype, memid, dtime, grossfine1, grossfine2, badlarate, badla, badlaon, badlanj, final, deliver, rmode, mainacno, lottid, ordid, stampid, issgwt, isswt, recgwt, recwt, losswt, lossfine, balwt, balfine, exwt, exfine, lbramt, khisab, stnwt, stnpc, diawt, diapc, stbeeds, alwstg, alfine, dustwt, tunchwt, isspc, recpc, avgtunch, lossdate, waxdiawt, waxstwt, stfine, extunch, meltunch, melwt, kadiwt, kpcwt, kadipc, konewt, bdscalc, ststnwt, ststnpc, flag, ratefix, crdays, rpamount, qttno, invdate, ebill, audit, chqid, bbid, billtype, daily, altvou, mhisab, billfine1, billfine2, billbal, adjfine1, adjfine2, adjbal, tgino, tgigroupid, tgptunch, tgpwstg, tgrate, tgmrate, tgspolish, tgstunch, tglbr, tgslbr, tgstkino, tgstampid, tgpcless, tgdsless, tgvalless, pamount, upflg, trdata, mainrecord, pdc, insflg, jobid, uphisab, oacno, jobno, ojobno, ojobid, designid, issfine, recfine, polosswt, polossfine, postnfine, bullid, tgppolish, mrnameid, exptax, acno12acno, exrateok, cdcharge, stax, dollar, metaltype, chqtrnid, inthisab, rtunch, wtunch, bankchg, postamt, postrate, postlbr, sumno, spolishwt, dustfine, orgacno, ino, trtrnid, tgset, orgcname, ledwt, opening, macno, tgbatch, tunchfine, kittyvono, postlfine, mcurrid, mcamount, xrate, mcxcno, appcode, cardtype, billnarr, badlaid, mcamount2, mcurrid1, mcurrid2, allot, fine3, fine4, op7, ugamount, ledgerflg, cntacno, prnflag, labid, dmtno, dmloginid, dmtime, del, comacno, comamt, mpoints, couponid, webid, mtranid, interest, notecntr, duedate, excise, schgamt, inthisabg, inthisabs, cppan, othfine, cashbank, cgst, sgst, igst, cpstateid, cpgstin, brid, irn, billinfo, itcstatus

## Table: tree1 (ID: 49596, Total Columns: 20)
Columns: treeid, tno, sno, basewt, treewt, waxwt, into, reqdwt, stnwt, stnpc, pc, refno, tdate, diawt, diapc, castwt, casttno, vono, stampid, daily

## Table: userlog (ID: 49617, Total Columns: 4)
Columns: sesid, loginid, timein, timeout

## Table: vou1 (ID: 49622, Total Columns: 20)
Columns: tno, tdate, seriesid, noseriesid, siteid, no12, vtype, billno, refno, acno, daily, dtime, loginid, upflg, insflg, trdata, ebill, opening, vouflg, invtno

## Table: vvono1 (ID: 49643, Total Columns: 3)
Columns: vono, tno, daily

## Table: Settings
Columns: SettingsName (TEXT 100, PK), SettingsValue (LONGTEXT)
Keys:
- `SavePath`: Default folder for export and invoice PDFs
- `WaSendMethod`: WhatsApp sending method (API, Web, Desktop, Disabled)
- `WaAPIKey`: WhatsApp gateway token/key
- `WaTemplate`: WhatsApp template text with variables
- `WaBusinessNo`: Sender WhatsApp phone number
- `WaAutoSend`: Automatically trigger WhatsApp on invoice save (true/false)
- `EinvoiceMode`: GST E-Invoice environment (DEMO / LIVE)
- `DefaultBillPrinter`: Default printer queue name for tax invoice printing
- `HideWtInPcItems`: Hide weight values for piece-rate items on print (true/false)
- `AllowNtWtModUpto`: Maximum allowed deviation (±g) when changing barcode Net Weight during billing (e.g. 0.010 allows a 2.320g item to change within 2.310g - 2.330g)
- Weight scale keys: `weight_enabled`, `weight_commport`, `weight_baudrate`, `weight_databits`, `weight_stopbits`, `weight_parity`, `weight_handshake`, `weight_stable_prefix`, `weight_timeout_ms`

